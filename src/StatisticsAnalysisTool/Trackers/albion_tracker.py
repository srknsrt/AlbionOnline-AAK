import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace', line_buffering=True)
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace', line_buffering=True)

from scapy.all import sniff, UDP, Raw, IP
import requests
import struct
import threading
from datetime import datetime
from collections import defaultdict

# ============================================
# SUPABASE AYARLARI
# ============================================
SUPABASE_URL = "https://fwaszogbepswybvtecrk.supabase.co"
SUPABASE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZ3YXN6b2diZXBzd3lidnRlY3JrIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc3MzE2ODQyMSwiZXhwIjoyMDg4NzQ0NDIxfQ.N0BZAf3i8S3j4PAnf7KrgeM7H6FID4AuRhQG3rqtikI"

HEADERS = {
    "apikey": SUPABASE_KEY,
    "Authorization": f"Bearer {SUPABASE_KEY}",
    "Content-Type": "application/json"
}

UPSERT_HEADERS = {**HEADERS, "Prefer": "resolution=merge-duplicates"}

# ============================================
# BELLEK BUFFER
# ============================================
stats_buffer = []
buffer_lock = threading.Lock()

# ============================================
# PROTOCOL 18 - TYPE KODLARI
# ============================================
P18_BOOLEAN      = 2
P18_BYTE         = 3
P18_SHORT        = 4
P18_FLOAT        = 5
P18_DOUBLE       = 6
P18_STRING       = 7
P18_NULL         = 8
P18_COMP_INT     = 9
P18_COMP_LONG    = 10
P18_INT1         = 11
P18_INT1_NEG     = 12
P18_INT2         = 13
P18_INT2_NEG     = 14
P18_LONG1        = 15
P18_LONG1_NEG    = 16
P18_LONG2        = 17
P18_LONG2_NEG    = 18
P18_CUSTOM       = 19
P18_DICT         = 20
P18_HASHTABLE    = 21
P18_OBJ_ARRAY    = 23
P18_OP_REQ       = 24
P18_OP_RESP      = 25
P18_EVENT_DATA   = 26
P18_BOOL_FALSE   = 27
P18_BOOL_TRUE    = 28
P18_SHORT_ZERO   = 29
P18_INT_ZERO     = 30
P18_LONG_ZERO    = 31
P18_FLOAT_ZERO   = 32
P18_DOUBLE_ZERO  = 33
P18_BYTE_ZERO    = 34
P18_ARRAY        = 64
P18_BOOL_ARRAY   = 66
P18_BYTE_ARRAY   = 67
P18_SHORT_ARRAY  = 68
P18_FLOAT_ARRAY  = 69
P18_DOUBLE_ARRAY = 70
P18_STR_ARRAY    = 71
P18_CINT_ARRAY   = 73
P18_CLONG_ARRAY  = 74
P18_CUSTOM_ARRAY = 83
P18_DICT_ARRAY   = 84
P18_HASH_ARRAY   = 85
P18_SLIM_BASE    = 128

# Photon command types
CMD_DISCONNECT     = 4
CMD_SEND_RELIABLE  = 6
CMD_SEND_UNRELI    = 7
CMD_SEND_FRAGMENT  = 8

# Message types
MSG_OP_REQUEST  = 2
MSG_OP_RESPONSE = 3
MSG_EVENT_DATA  = 4

# CharacterStats event kodu
EVENT_CHARACTER_STATS = 149

# ============================================
# PROTOCOL 18 STREAM OKUYUCU
# ============================================
class P18Stream:
    def __init__(self, data, offset=0):
        self.data = data
        self.pos = offset

    def has_more(self):
        return self.pos < len(self.data)

    def remaining(self):
        return len(self.data) - self.pos

    def read_byte(self):
        if self.pos >= len(self.data):
            raise EOFError("P18Stream bitti")
        b = self.data[self.pos]
        self.pos += 1
        return b

    def read_bytes(self, n):
        if self.pos + n > len(self.data):
            raise EOFError(f"P18Stream: {n} byte lazim, {self.remaining()} kaldi")
        b = self.data[self.pos:self.pos + n]
        self.pos += n
        return b

    def read_short(self):
        b = self.read_bytes(2)
        return struct.unpack('<H', b)[0]  # Little-endian (Protocol 18)

    def read_float(self):
        b = self.read_bytes(4)
        return struct.unpack('<f', b)[0]

    def read_double(self):
        b = self.read_bytes(8)
        return struct.unpack('<d', b)[0]

    def read_comp_uint32(self):
        value, shift = 0, 0
        while shift < 35:
            b = self.read_byte()
            value |= (b & 0x7F) << shift
            shift += 7
            if (b & 0x80) == 0:
                return value
        return value

    def read_comp_uint64(self):
        value, shift = 0, 0
        while shift < 70:
            b = self.read_byte()
            value |= (b & 0x7F) << shift
            shift += 7
            if (b & 0x80) == 0:
                return value
        return value

    def read_comp_int32(self):
        n = self.read_comp_uint32()
        return (n >> 1) ^ -(n & 1)

    def read_comp_int64(self):
        n = self.read_comp_uint64()
        return (n >> 1) ^ -(n & 1)

    def read_string(self):
        length = self.read_comp_uint32()
        if length == 0:
            return ""
        return self.read_bytes(length).decode('utf-8', errors='ignore')

    def skip(self, n):
        self.pos = min(self.pos + n, len(self.data))

def p18_deserialize(stream):
    try:
        type_code = stream.read_byte()
        return _p18_deserialize_typed(stream, type_code)
    except EOFError:
        return None

def _p18_deserialize_typed(stream, type_code):
    if type_code >= P18_SLIM_BASE:
        # CustomTypeSlim
        length = stream.read_comp_uint32()
        stream.skip(length)
        return None

    if type_code == P18_NULL:       return None
    if type_code == P18_BOOL_FALSE: return False
    if type_code == P18_BOOL_TRUE:  return True
    if type_code == P18_BOOLEAN:    return stream.read_byte() != 0
    if type_code == P18_BYTE_ZERO:  return 0
    if type_code == P18_BYTE:       return stream.read_byte()
    if type_code == P18_SHORT_ZERO: return 0
    if type_code == P18_SHORT:      return stream.read_short()
    if type_code == P18_FLOAT_ZERO: return 0.0
    if type_code == P18_FLOAT:      return stream.read_float()
    if type_code == P18_DOUBLE_ZERO:return 0.0
    if type_code == P18_DOUBLE:     return stream.read_double()
    if type_code == P18_INT_ZERO:   return 0
    if type_code == P18_LONG_ZERO:  return 0
    if type_code == P18_INT1:       return stream.read_byte()
    if type_code == P18_INT1_NEG:   return -stream.read_byte()
    if type_code == P18_INT2:       return stream.read_short()
    if type_code == P18_INT2_NEG:   return -stream.read_short()
    if type_code == P18_LONG1:      return stream.read_byte()
    if type_code == P18_LONG1_NEG:  return -stream.read_byte()
    if type_code == P18_LONG2:      return stream.read_short()
    if type_code == P18_LONG2_NEG:  return -stream.read_short()
    if type_code == P18_COMP_INT:   return stream.read_comp_int32()
    if type_code == P18_COMP_LONG:  return stream.read_comp_int64()
    if type_code == P18_STRING:     return stream.read_string()

    if type_code == P18_BYTE_ARRAY:
        n = stream.read_comp_uint32()
        stream.skip(n)
        return None

    if type_code == P18_SHORT_ARRAY:
        n = stream.read_comp_uint32()
        stream.skip(n * 2)
        return None

    if type_code == P18_FLOAT_ARRAY:
        n = stream.read_comp_uint32()
        stream.skip(n * 4)
        return None

    if type_code == P18_DOUBLE_ARRAY:
        n = stream.read_comp_uint32()
        stream.skip(n * 8)
        return None

    if type_code == P18_BOOL_ARRAY:
        n = stream.read_comp_uint32()
        stream.skip((n + 7) // 8)
        return None

    if type_code == P18_STR_ARRAY:
        n = stream.read_comp_uint32()
        for _ in range(n):
            stream.read_string()
        return None

    if type_code == P18_CINT_ARRAY:
        n = stream.read_comp_uint32()
        for _ in range(n):
            stream.read_comp_int32()
        return None

    if type_code == P18_CLONG_ARRAY:
        n = stream.read_comp_uint32()
        for _ in range(n):
            stream.read_comp_int64()
        return None

    if type_code == P18_OBJ_ARRAY:
        n = stream.read_comp_uint32()
        for _ in range(n):
            p18_deserialize(stream)
        return None

    if type_code == P18_ARRAY:
        n = stream.read_comp_uint32()
        inner_type = stream.read_byte()
        for _ in range(n):
            _p18_deserialize_typed(stream, inner_type)
        return None

    if type_code == P18_HASHTABLE:
        n = stream.read_comp_uint32()
        result = {}
        for _ in range(n):
            k = p18_deserialize(stream)
            v = p18_deserialize(stream)
            if k is not None:
                result[k] = v
        return result

    if type_code == P18_DICT:
        # key type + value type
        key_type = stream.read_byte()
        val_type = stream.read_byte()
        n = stream.read_comp_uint32()
        result = {}
        for _ in range(n):
            k_type = key_type if key_type != 0 else stream.read_byte()
            k = _p18_deserialize_typed(stream, k_type)
            v_type = val_type if val_type != 0 else stream.read_byte()
            v = _p18_deserialize_typed(stream, v_type)
            if k is not None:
                result[k] = v
        return result

    if type_code == P18_CUSTOM:
        stream.read_byte()  # custom type code
        length = stream.read_comp_uint32()
        stream.skip(length)
        return None

    if type_code in (P18_OP_REQ, P18_OP_RESP, P18_EVENT_DATA):
        # Recursive structure
        return None

    # Unknown type — skip is unsafe, return None
    return None


def p18_read_param_table(stream):
    params = {}
    try:
        count = stream.read_byte()
        for _ in range(count):
            key = stream.read_byte()
            type_code = stream.read_byte()
            value = _p18_deserialize_typed(stream, type_code)
            params[key] = value
    except EOFError:
        pass
    return params


# ============================================
# FRAGMENT BİRLEŞTİRME (Protocol 18)
# ============================================
frag_buffers = {}   # key -> {'total': int, 'data': bytearray, 'received': int}

def fragment_isle(ip_src, ip_dst, start_seq, total_length, frag_offset, payload):
    key = (ip_src, ip_dst, start_seq)
    if key not in frag_buffers:
        frag_buffers[key] = {
            'total': total_length,
            'data': bytearray(total_length),
            'received': 0,
            'ts': datetime.now().timestamp()
        }
    buf = frag_buffers[key]
    end = frag_offset + len(payload)
    if end <= total_length:
        buf['data'][frag_offset:end] = payload
        buf['received'] += len(payload)

    if buf['received'] >= total_length:
        tam_veri = bytes(buf['data'])
        del frag_buffers[key]
        return tam_veri

    # Temizlik: 30 sn'den eski fragmanları sil
    simdi = datetime.now().timestamp()
    eski = [k for k, v in frag_buffers.items() if simdi - v['ts'] > 30]
    for k in eski:
        del frag_buffers[k]

    return None


# ============================================
# PHOTON PAKETİ İŞLE (Protocol 18)
# ============================================
def photon_isle(ip_src, ip_dst, veri, sonuc_callback):
    if len(veri) < 12:
        return

    offset = 0
    # PhotonHeader: peer_id(2) + flags(1) + commandCount(1) + timestamp(4) + challenge(4)
    flags = veri[2]
    command_count = veri[3]
    offset = 12

    # Encrypted paketleri atla
    if flags == 1:
        return

    for _ in range(command_count):
        if offset + 12 > len(veri):
            break
        cmd_type    = veri[offset]
        cmd_length  = struct.unpack('>i', veri[offset+4:offset+8])[0]
        cmd_payload_start = offset + 12
        cmd_payload_end   = offset + cmd_length

        if cmd_payload_end > len(veri) or cmd_length < 12:
            break

        if cmd_type == CMD_SEND_RELIABLE:
            # [+12]: skip 1, [+13]: messageType, [+14+]: payload
            if cmd_payload_start + 2 <= cmd_payload_end:
                msg_type = veri[cmd_payload_start + 1]
                payload = veri[cmd_payload_start + 2:cmd_payload_end]
                _msg_isle(msg_type, payload, sonuc_callback)

        elif cmd_type == CMD_SEND_UNRELI:
            # [+12]: skip 4 (unreliable seq) + skip 1 + messageType + payload
            if cmd_payload_start + 6 <= cmd_payload_end:
                msg_type = veri[cmd_payload_start + 5]
                payload = veri[cmd_payload_start + 6:cmd_payload_end]
                _msg_isle(msg_type, payload, sonuc_callback)

        elif cmd_type == CMD_SEND_FRAGMENT:
            # [+12]: startSeq(4) + ?(4) + ?(4) + totalLength(4) + fragOffset(4) + data
            if cmd_payload_start + 20 <= cmd_payload_end:
                start_seq    = struct.unpack('>i', veri[cmd_payload_start+0:cmd_payload_start+4])[0]
                total_length = struct.unpack('>i', veri[cmd_payload_start+12:cmd_payload_start+16])[0]
                frag_offset  = struct.unpack('>i', veri[cmd_payload_start+16:cmd_payload_start+20])[0]
                frag_data    = veri[cmd_payload_start+20:cmd_payload_end]

                tam_veri = fragment_isle(ip_src, ip_dst, start_seq, total_length, frag_offset, frag_data)
                if tam_veri:
                    # Birleşmiş veri bir mesaj — skip 1 + messageType + payload
                    if len(tam_veri) >= 2:
                        msg_type = tam_veri[1]
                        _msg_isle(msg_type, tam_veri[2:], sonuc_callback)

        offset += cmd_length


def _msg_isle(msg_type, payload, callback):
    if len(payload) < 1:
        return

    stream = P18Stream(payload)
    try:
        if msg_type == MSG_EVENT_DATA:
            event_code = stream.read_byte()
            print(f"[DEBUG] EventData code={event_code} (0x{event_code:02x}) payload={len(payload)}b")
            if event_code == EVENT_CHARACTER_STATS:
                params = p18_read_param_table(stream)
                callback(params)
        elif msg_type == MSG_OP_RESPONSE:
            op_code = stream.read_byte()
            print(f"[DEBUG] OpResponse code={op_code} (0x{op_code:02x}) payload={len(payload)}b")
    except Exception as e:
        print(f"[DEBUG] msg_isle hata: {e}")


# ============================================
# STATS PARAMETRELERINI PARSE ET
# ============================================
GUILD_ADI = "At Arayan Kelebekler"
GECERSIZ  = {"At Arayan Kelebekler", ""}

# Param key'leri (Protocol 16 ile aynı)
FAME_KEYS = {
    'total':  [0x07, 0x06, 0x08],
    'kill':   [0x0b, 0x0a, 0x0c],
    'pve':    [0x0d, 0x0c, 0x0e],
    'gather': [0x0e, 0x0f, 0x10],
    'craft':  [0x10, 0x11, 0x12],
}

def long_deger(params, keyler):
    for k in keyler:
        v = params.get(k)
        if isinstance(v, (int, float)) and v > 0:
            return int(v) // 10000
    return 0

def stats_isle(params):
    try:
        isim  = params.get(0x01, "")
        guild = params.get(0x02, "")

        if not isinstance(isim, str) or not isim or isim in GECERSIZ:
            return
        if len(isim) < 2 or (" " in isim and len(isim) > 15):
            return
        if guild != GUILD_ADI:
            return

        total_fame  = long_deger(params, FAME_KEYS['total'])
        kill_fame   = long_deger(params, FAME_KEYS['kill'])
        pve_fame    = long_deger(params, FAME_KEYS['pve'])
        gather_fame = long_deger(params, FAME_KEYS['gather'])
        craft_fame  = long_deger(params, FAME_KEYS['craft'])

        if total_fame == 0 and (kill_fame + pve_fame + gather_fame + craft_fame) > 0:
            total_fame = kill_fame + pve_fame + gather_fame + craft_fame

        kills_val = params.get(0x0a, 0)
        total_kills = int(kills_val) if isinstance(kills_val, (int, float)) else 0

        sonuc = {
            "player_name": isim,
            "guild": guild,
            "total_fame": total_fame,
            "kill_fame": kill_fame,
            "pve_fame": pve_fame,
            "gather_fame": gather_fame,
            "craft_fame": craft_fame,
            "total_kills": total_kills,
            "recorded_at": datetime.now().isoformat()
        }

        with buffer_lock:
            stats_buffer[:] = [s for s in stats_buffer if s['player_name'] != isim]
            stats_buffer.append(sonuc)

        print(f"\n{isim} | Guild: {guild}")
        print(f"   Total: {total_fame:,} | Kill: {kill_fame:,} | PvE: {pve_fame:,}")
        print(f"   Gather: {gather_fame:,} | Craft: {craft_fame:,} | Kills: {total_kills}")
        with buffer_lock:
            print(f"   Buffer: {len(stats_buffer)} oyuncu bekliyor")

    except Exception as e:
        print(f"[Parse hatasi: {e}]")


# ============================================
# VERİTABANINA AKTAR
# ============================================
def uye_guncelle(oyuncu_adi):
    url = f"{SUPABASE_URL}/rest/v1/guild_members"
    kontrol = requests.get(f"{url}?player_name=eq.{oyuncu_adi}", headers=HEADERS, timeout=10).json()
    if len(kontrol) == 0:
        requests.post(url, headers=HEADERS, json={
            "player_name": oyuncu_adi, "is_active": True,
            "first_seen": datetime.now().isoformat(), "last_seen": datetime.now().isoformat()
        }, timeout=10)
    else:
        requests.patch(f"{url}?player_name=eq.{oyuncu_adi}", headers=HEADERS,
            json={"is_active": True, "last_seen": datetime.now().isoformat()}, timeout=10)

def player_stats_kaydet(veri):
    url = f"{SUPABASE_URL}/rest/v1/player_stats"
    player = veri['player_name']
    bugun = datetime.now().strftime('%Y-%m-%d')
    try:
        kontrol = requests.get(
            f"{url}?player_name=eq.{player}&recorded_at=gte.{bugun}T00:00:00&recorded_at=lt.{bugun}T23:59:59&limit=1",
            headers=HEADERS, timeout=10)
        if kontrol.status_code == 200 and kontrol.json():
            mevcut = kontrol.json()[0]
            degisti = any(mevcut.get(a, 0) != veri.get(a, 0)
                         for a in ['total_fame','kill_fame','pve_fame','gather_fame','craft_fame','total_kills'])
            if not degisti:
                return "skip"
            kayit_id = mevcut.get('id')
            if kayit_id:
                kayit = {k: v for k, v in veri.items() if k != 'guild'}
                requests.patch(f"{url}?id=eq.{kayit_id}", headers=HEADERS, json=kayit, timeout=10)
                return "update"
    except Exception:
        pass
    kayit = {k: v for k, v in veri.items() if k != 'guild'}
    requests.post(url, headers=HEADERS, json=kayit, timeout=10)
    return "insert"

def veritabanina_aktar():
    with buffer_lock:
        if not stats_buffer:
            print("[Buffer bos, aktarilacak veri yok]")
            return
        kopya = stats_buffer.copy()
        stats_buffer.clear()

    print(f"\n{'='*50}")
    print(f"{len(kopya)} kayit veritabanina aktariliyor...")
    eklenen = guncellenen = atlanan = hatali = 0
    for sonuc in kopya:
        try:
            uye_guncelle(sonuc['player_name'])
            islem = player_stats_kaydet(sonuc)
            if islem == "skip":
                print(f"  - {sonuc['player_name']} | {sonuc['total_fame']:,} (degisiklik yok)")
                atlanan += 1
            elif islem == "update":
                print(f"  ~ {sonuc['player_name']} | {sonuc['total_fame']:,} (guncellendi)")
                guncellenen += 1
            else:
                print(f"  + {sonuc['player_name']} | {sonuc['total_fame']:,} (yeni)")
                eklenen += 1
        except Exception as e:
            print(f"  X {sonuc['player_name']} hata: {e}")
            hatali += 1
    print(f"Tamamlandi: {eklenen} yeni, {guncellenen} guncelleme, {atlanan} ayni, {hatali} hata")
    print(f"{'='*50}\n")


# ============================================
# STDIN DİNLE (FLUSH komutu için)
# ============================================
def stdin_dinle():
    for line in sys.stdin:
        komut = line.strip().upper()
        if komut == "FLUSH":
            veritabanina_aktar()
        elif komut == "STATUS":
            with buffer_lock:
                print(f"[Buffer: {len(stats_buffer)} kayit bekliyor]")

stdin_thread = threading.Thread(target=stdin_dinle, daemon=True)
stdin_thread.start()


# ============================================
# UDP PAKETLERİ DİNLE
# ============================================
def paketi_isle(paket):
    try:
        if not (paket.haslayer(UDP) and paket.haslayer(Raw) and paket.haslayer(IP)):
            return
        veri   = bytes(paket[Raw].load)
        ip_src = paket[IP].src
        ip_dst = paket[IP].dst
        photon_isle(ip_src, ip_dst, veri, stats_isle)
    except Exception:
        pass


print("Albion Stats Tracker baslatiliyor... (Protocol 18)")
print(f"Guild: {GUILD_ADI}")
print("Mod: BUFFER (Veritabanina Aktar butonuna basana kadar bekler)")
print("\nAlbion'da guild uyesine sag tikla -> Stats de!\n")

try:
    sniff(filter="udp and (port 5055 or port 5056)", prn=paketi_isle, store=0)
except KeyboardInterrupt:
    print("\nTracker durduruldu.")
except Exception as e:
    print(f"Hata: {e}")
