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

# ============================================
# BELLEK BUFFER - Paketler burada birikir
# ============================================
stats_buffer = []
buffer_lock = threading.Lock()

# ============================================
# PHOTON FRAGMENT BİRLEŞTİRME
# ============================================
fragment_buffer = defaultdict(dict)
fragment_zaman = {}

def fragment_temizle():
    simdi = datetime.now().timestamp()
    eski = [k for k, t in fragment_zaman.items() if simdi - t > 30]
    for k in eski:
        fragment_buffer.pop(k, None)
        fragment_zaman.pop(k, None)

def fragment_birlestir(ip_src, ip_dst, veri):
    tum_paketler = []
    try:
        if len(veri) < 12:
            return [veri]
        offset = 12
        command_count = veri[3]
        for _ in range(command_count):
            if offset >= len(veri): break
            cmd_type = veri[offset]
            if offset + 12 > len(veri): break
            cmd_length = struct.unpack('>i', veri[offset+4:offset+8])[0]
            if cmd_length <= 0 or cmd_length > 65535: break
            if cmd_type == 8:
                if offset + 28 > len(veri): break
                start_seq  = struct.unpack('>i', veri[offset+12:offset+16])[0]
                frag_count = struct.unpack('>i', veri[offset+16:offset+20])[0]
                frag_no    = struct.unpack('>i', veri[offset+20:offset+24])[0]
                payload    = veri[offset+28:offset+cmd_length]
                key = (ip_src, ip_dst, start_seq)
                fragment_buffer[key][frag_no] = payload
                fragment_zaman[key] = datetime.now().timestamp()
                if len(fragment_buffer[key]) == frag_count:
                    tam_veri = b''.join(fragment_buffer[key].get(i, b'') for i in range(frag_count))
                    del fragment_buffer[key]
                    fragment_zaman.pop(key, None)
                    tum_paketler.append(tam_veri)
            elif cmd_type in (6, 7):
                tum_paketler.append(veri[offset+12:offset+cmd_length])
            offset += cmd_length
    except Exception:
        tum_paketler.append(veri)
    if len(fragment_buffer) > 50:
        fragment_temizle()
    return tum_paketler if tum_paketler else [veri]

# ============================================
# STATS PAKETİNİ PARSE ET
# ============================================
FAME_PARAM_ALTERNATES = {
    'total': [0x07, 0x06, 0x08],
    'kill':  [0x0b, 0x0a, 0x0c],
    'pve':   [0x0d, 0x0c, 0x0e],
    'gather':[0x0e, 0x0f, 0x10],
    'craft': [0x10, 0x11, 0x12],
}

def fame_bul(fame_map, anahtar_listesi, bolme=10000):
    for key in anahtar_listesi:
        val = fame_map.get(key, 0)
        if val > 0:
            return val // bolme
    return 0

def parse_stats_paketi(veri):
    try:
        idx = veri.find(b'\x01s\x00')
        if idx == -1: return None
        isim_uzunluk = veri[idx + 3]
        isim = veri[idx + 4: idx + 4 + isim_uzunluk].decode('utf-8', errors='ignore')

        guild_idx = veri.find(b'\x02s\x00')
        guild = ""
        if guild_idx != -1:
            guild_uzunluk = veri[guild_idx + 3]
            guild = veri[guild_idx + 4: guild_idx + 4 + guild_uzunluk].decode('utf-8', errors='ignore')

        fame_map = {}
        byte_map = {}
        short_map = {}
        int_map = {}
        idx2 = 0
        while idx2 < len(veri) - 10:
            param_no = veri[idx2]
            tip = veri[idx2 + 1]
            if tip == 0x6c:
                fame_map[param_no] = struct.unpack('>q', veri[idx2+2:idx2+10])[0]
                idx2 += 10
            elif tip == 0x69:
                int_map[param_no] = struct.unpack('>i', veri[idx2+2:idx2+6])[0]
                fame_map[param_no] = int_map[param_no]
                idx2 += 6
            elif tip == 0x6b:
                short_map[param_no] = struct.unpack('>h', veri[idx2+2:idx2+4])[0]
                idx2 += 4
            elif tip == 0x62:
                byte_map[param_no] = veri[idx2+2]
                idx2 += 3
            else:
                idx2 += 1

        total_fame  = fame_bul(fame_map, FAME_PARAM_ALTERNATES['total'])
        kill_fame   = fame_bul(fame_map, FAME_PARAM_ALTERNATES['kill'])
        pve_fame    = fame_bul(fame_map, FAME_PARAM_ALTERNATES['pve'])
        gather_fame = fame_bul(fame_map, FAME_PARAM_ALTERNATES['gather'])
        craft_fame  = fame_bul(fame_map, FAME_PARAM_ALTERNATES['craft'])

        # Fallback: total 0 ama alt kategoriler doluysa topla
        if total_fame == 0 and (kill_fame + pve_fame + gather_fame + craft_fame) > 0:
            total_fame = kill_fame + pve_fame + gather_fame + craft_fame

        total_kills = short_map.get(0x0a, None)
        if total_kills is None:
            total_kills = byte_map.get(0x0a, 0)

        GECERSIZ = ["At Arayan Kelebekler", ""]
        if not isim or len(isim) < 2 or isim in GECERSIZ:
            return None
        if " " in isim and len(isim) > 15:
            return None

        return {
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
    except Exception as e:
        print(f"Parse hatasi: {e}")
    return None

# ============================================
# VERİTABANINA AKTAR (UPSERT)
# ============================================
UPSERT_HEADERS = {
    "apikey": SUPABASE_KEY,
    "Authorization": f"Bearer {SUPABASE_KEY}",
    "Content-Type": "application/json",
    "Prefer": "resolution=merge-duplicates"
}

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

    # Bugunun kaydini kontrol et
    try:
        kontrol = requests.get(
            f"{url}?player_name=eq.{player}&recorded_at=gte.{bugun}T00:00:00&recorded_at=lt.{bugun}T23:59:59&limit=1",
            headers=HEADERS, timeout=10)
        if kontrol.status_code == 200 and kontrol.json():
            mevcut = kontrol.json()[0]
            # Degisiklik var mi kontrol et
            degisti = False
            for alan in ['total_fame', 'kill_fame', 'pve_fame', 'gather_fame', 'craft_fame', 'total_kills']:
                if mevcut.get(alan, 0) != veri.get(alan, 0):
                    degisti = True
                    break
            if not degisti:
                return "skip"
            # Bugunun kaydini guncelle
            kayit_id = mevcut.get('id')
            if kayit_id:
                kayit = {k: v for k, v in veri.items() if k != 'guild'}
                requests.patch(f"{url}?id=eq.{kayit_id}", headers=HEADERS, json=kayit, timeout=10)
                return "update"
    except Exception:
        pass

    # Bugun icin yeni kayit ekle
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
    eklenen = 0
    guncellenen = 0
    atlanan = 0
    hatali = 0
    for sonuc in kopya:
        try:
            uye_guncelle(sonuc['player_name'])
            islem = player_stats_kaydet(sonuc)
            if islem == "skip":
                print(f"  - {sonuc['player_name']} | Total: {sonuc['total_fame']:,} (degisiklik yok)")
                atlanan += 1
            elif islem == "update":
                print(f"  ~ {sonuc['player_name']} | Total: {sonuc['total_fame']:,} (guncellendi)")
                guncellenen += 1
            else:
                print(f"  + {sonuc['player_name']} | Total: {sonuc['total_fame']:,} (yeni)")
                eklenen += 1
        except Exception as e:
            print(f"  X {sonuc['player_name']} hata: {e}")
            hatali += 1
    print(f"Tamamlandi: {eklenen} yeni, {guncellenen} guncelleme, {atlanan} degisiklik yok, {hatali} hata")
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
GUILD_ADI = "At Arayan Kelebekler"

def paketi_isle(paket):
    try:
        if not (paket.haslayer(UDP) and paket.haslayer(Raw) and paket.haslayer(IP)):
            return
        veri = bytes(paket[Raw].load)
        ip_src = paket[IP].src
        ip_dst = paket[IP].dst
        for islenmis_veri in fragment_birlestir(ip_src, ip_dst, veri):
            if GUILD_ADI.encode() in islenmis_veri and len(islenmis_veri) > 150:
                sonuc = parse_stats_paketi(islenmis_veri)
                if sonuc:
                    with buffer_lock:
                        # Ayni oyuncunun eski kaydini guncelle
                        stats_buffer[:] = [s for s in stats_buffer if s['player_name'] != sonuc['player_name']]
                        stats_buffer.append(sonuc)
                    print(f"\n{sonuc['player_name']} | Guild: {sonuc['guild']}")
                    print(f"   Total: {sonuc['total_fame']:,} | Kill: {sonuc['kill_fame']:,} | PvE: {sonuc['pve_fame']:,}")
                    print(f"   Gather: {sonuc['gather_fame']:,} | Craft: {sonuc['craft_fame']:,} | Kills: {sonuc['total_kills']}")
                    with buffer_lock:
                        print(f"   Buffer: {len(stats_buffer)} oyuncu bekliyor")
    except Exception:
        pass

print("Albion Stats Tracker baslatiliyor...")
print(f"Guild: {GUILD_ADI}")
print("Mod: BUFFER (Veritabanina Aktar butonuna basana kadar bekler)")
print("\nAlbion'da guild uyesine sag tikla -> Stats de!\n")

try:
    sniff(filter="udp and (port 5055 or port 5056)", prn=paketi_isle, store=0)
except KeyboardInterrupt:
    print("\nTracker durduruldu.")
except Exception as e:
    print(f"Hata: {e}")
