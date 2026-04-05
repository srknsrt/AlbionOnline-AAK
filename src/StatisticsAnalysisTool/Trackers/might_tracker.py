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

KATEGORI_MAP = {
    "PVE":         "PvE (Outlands and Roads)",
    "GATHERING":   "Gathering (Outlands and Roads)",
    "POWERCORE":   "Hideout Power Cores",
    "SMUGGLERS":   "Smugglers",
    "HELLGATE":    "Hellgates",
    "HELLDUNGEON": "The Depths",
    "CORRUPTED":   "Corrupted Dungeons",
    "CASTLE":      "Castles & Castle Outposts",
    "SPIDERS":     "Crystal Creatures",
    "TREASURES":   "Outlands Treasures",
    "TERRITORY":   "Territory Power Crystals",
    "SIPHONING":   "Siphoning Mages",
}

# ============================================
# BELLEK BUFFER
# ============================================
# {(player_name, kategori_kodu): {amount, rank}}
might_buffer = {}
level_buffer = []
buffer_lock = threading.Lock()

# Sayac: kategorilerdeki toplam okuma
kategori_sayac = defaultdict(int)

# ============================================
# FRAGMENT BİRLEŞTİRME
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
        if len(veri) < 12: return [veri]
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
                    tam_veri = b''.join(fragment_buffer[key][i] for i in range(frag_count))
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

def stringleri_cek(veri):
    strings = []
    i = 0
    while i < len(veri) - 4:
        if veri[i] == 0x73:
            try:
                uzunluk = struct.unpack('>H', veri[i+1:i+3])[0]
                if 2 <= uzunluk <= 50:
                    s = veri[i+3:i+3+uzunluk].decode('utf-8')
                    if s.isprintable():
                        strings.append((i, s))
                        i += 3 + uzunluk
                        continue
            except Exception:
                pass
        i += 1
    return strings

def dizi_oku(veri, pos):
    if pos + 4 > len(veri): return None
    if veri[pos] != 0x79 or veri[pos+1] != 0x00: return None
    count = veri[pos+2]
    tip   = veri[pos+3]
    if count == 0 or count > 200: return None
    cur = pos + 4
    items = []
    if tip == 0x73:
        for _ in range(count):
            if cur + 2 > len(veri): break
            length = struct.unpack(">H", veri[cur:cur+2])[0]
            if length == 0 or cur + 2 + length > len(veri): break
            try: items.append(veri[cur+2:cur+2+length].decode('utf-8'))
            except: break
            cur += 2 + length
    elif tip == 0x6c:
        for _ in range(count):
            if cur + 8 > len(veri): break
            items.append(struct.unpack(">q", veri[cur:cur+8])[0])
            cur += 8
    elif tip == 0x69:
        for _ in range(count):
            if cur + 4 > len(veri): break
            items.append(struct.unpack(">i", veri[cur:cur+4])[0])
            cur += 4
    return (count, tip, items) if items else None

# ============================================
# VERİTABANINA AKTAR (UPSERT)
# ============================================
def veritabanina_aktar():
    with buffer_lock:
        if not might_buffer and not level_buffer:
            print("[Buffer bos, aktarilacak veri yok]")
            return
        might_kopya = dict(might_buffer)
        level_kopya = list(level_buffer)
        might_buffer.clear()
        level_buffer.clear()

    print(f"\n{'='*55}")
    print(f"Veritabanina aktariliyor...")

    # Level'lari kaydet
    for entry in level_kopya:
        try:
            kategori_adi = KATEGORI_MAP.get(entry['kod'], entry['kod'])
            r = requests.post(f"{SUPABASE_URL}/rest/v1/guild_might", headers=HEADERS,
                json={"category": kategori_adi, "level": entry['level'], "recorded_at": datetime.now().isoformat()},
                timeout=10)
            print(f"  Level: {kategori_adi} -> {entry['level']}")
        except Exception as e:
            print(f"  X Level kayit hatasi: {e}")

    # Might kayitlarini kaydet (upsert)
    eklenen = 0
    guncellenen = 0
    atlanan = 0
    hatali = 0
    for (player_name, kategori_kodu), data in might_kopya.items():
        try:
            kategori_adi = KATEGORI_MAP.get(kategori_kodu, kategori_kodu)
            kontrol = requests.get(
                f"{SUPABASE_URL}/rest/v1/season_might?player_name=eq.{player_name}&category=eq.{kategori_adi}",
                headers=HEADERS, timeout=10)
            if kontrol.status_code == 200 and kontrol.json():
                mevcut = kontrol.json()[0]
                if mevcut.get("amount", 0) != data['amount']:
                    requests.patch(
                        f"{SUPABASE_URL}/rest/v1/season_might?player_name=eq.{player_name}&category=eq.{kategori_adi}",
                        headers=HEADERS,
                        json={"amount": data['amount'], "rank": data['rank'], "recorded_at": datetime.now().isoformat()},
                        timeout=10)
                    print(f"  ~ {data['rank']:3}. {player_name:<20} {data['amount']:>12,} (guncellendi)")
                    guncellenen += 1
                else:
                    print(f"  - {data['rank']:3}. {player_name:<20} {data['amount']:>12,} (degisiklik yok)")
                    atlanan += 1
            else:
                requests.post(f"{SUPABASE_URL}/rest/v1/season_might", headers=HEADERS,
                    json={"player_name": player_name, "category": kategori_adi,
                          "amount": data['amount'], "rank": data['rank'],
                          "recorded_at": datetime.now().isoformat()},
                    timeout=10)
                print(f"  + {data['rank']:3}. {player_name:<20} {data['amount']:>12,} (yeni)")
                eklenen += 1
        except Exception as e:
            print(f"  X {player_name} hata: {e}")
            hatali += 1

    print(f"Tamamlandi: {eklenen} yeni, {guncellenen} guncelleme, {atlanan} degisiklik yok, {hatali} hata")
    print(f"{'='*55}\n")

# ============================================
# STDIN DİNLE
# ============================================
def stdin_dinle():
    for line in sys.stdin:
        komut = line.strip().upper()
        if komut == "FLUSH":
            veritabanina_aktar()
        elif komut == "STATUS":
            with buffer_lock:
                print(f"[Buffer: {len(might_buffer)} might + {len(level_buffer)} level bekliyor]")

stdin_thread = threading.Thread(target=stdin_dinle, daemon=True)
stdin_thread.start()

# ============================================
# PARSE
# ============================================
def parse_level_paketi(veri):
    strings = stringleri_cek(veri)
    levelup_entries = [(idx, s) for idx, s in strings if s.startswith("MightCategoryLevelUp@")]
    if not levelup_entries: return
    for idx, s in levelup_entries:
        kod = s.replace("MightCategoryLevelUp@", "")
        level = 0
        for back in range(1, 20):
            if idx - back >= 0:
                val = veri[idx - back]
                if 1 <= val <= 50:
                    level = val
                    break
        with buffer_lock:
            level_buffer.append({'kod': kod, 'level': level})
        print(f"  Level buffer'a eklendi: {KATEGORI_MAP.get(kod, kod)} -> {level}")

def parse_oyuncu_array_paketi(veri):
    strings_all = stringleri_cek(veri)
    kategori_kodu = None
    for _, s in strings_all:
        if s in KATEGORI_MAP:
            kategori_kodu = s
            break
    if not kategori_kodu: return

    names = []
    amounts = []
    pos = 0
    while pos < len(veri) - 4:
        if veri[pos] == 0x79 and veri[pos+1] == 0x00:
            sonuc = dizi_oku(veri, pos)
            if sonuc:
                count, tip, items = sonuc
                if tip == 0x73:
                    if all(s in KATEGORI_MAP for s in items):
                        pos += 1
                        continue
                    names = items
                elif tip in (0x6c, 0x69):
                    amounts = items
        pos += 1

    if not names or not amounts: return

    baslangic_rank = 1
    pos = 0
    while pos < len(veri) - 3:
        if veri[pos] == 0x05 and veri[pos+1] == 0x62:
            rank_val = veri[pos+2]
            if 0 <= rank_val <= 500:
                baslangic_rank = rank_val + 1
                break
        pos += 1

    kategori_adi = KATEGORI_MAP[kategori_kodu]
    oyuncu_sayisi = min(len(names), len(amounts))

    # Kategori sayacini guncelle
    kategori_sayac[kategori_kodu] += oyuncu_sayisi

    print(f"\n{'='*55}")
    print(f"{kategori_adi} - {oyuncu_sayisi} oyuncu okundu")
    print(f"{'-'*55}")

    eklenen = 0
    with buffer_lock:
        for i, (isim, amt) in enumerate(zip(names, amounts)):
            rank = baslangic_rank + i
            gercek_miktar = int(amt / 10000)
            if gercek_miktar > 0:
                might_buffer[(isim, kategori_kodu)] = {'amount': gercek_miktar, 'rank': rank}
                print(f"  {rank:3}. {isim:<20} {gercek_miktar:>12,}")
                eklenen += 1

        print(f"{'-'*55}")
        print(f"  Bu okuma: {eklenen} oyuncu | Toplam buffer: {len(might_buffer)} kayit")

    # Kategori ozeti
    print(f"\n  Kategori ozeti:")
    for kod, sayi in sorted(kategori_sayac.items()):
        adi = KATEGORI_MAP.get(kod, kod)
        print(f"    {adi}: {sayi} oyuncu okundu")
    print(f"{'='*55}")

KATEGORI_KODLARI = set(KATEGORI_MAP.keys())

def paketi_isle(paket):
    try:
        if not (paket.haslayer(UDP) and paket.haslayer(Raw) and paket.haslayer(IP)): return
        veri = bytes(paket[Raw].load)
        ip_src = paket[IP].src
        ip_dst = paket[IP].dst
        for islenmis_veri in fragment_birlestir(ip_src, ip_dst, veri):
            if len(islenmis_veri) < 100: continue
            if b'MightCategoryLevelUp' in islenmis_veri:
                parse_level_paketi(islenmis_veri)
            else:
                for kod in KATEGORI_KODLARI:
                    if kod.encode() in islenmis_veri and b'\x79\x00' in islenmis_veri:
                        parse_oyuncu_array_paketi(islenmis_veri)
                        break
    except Exception:
        pass

print("Season Might Tracker baslatiliyor...")
print("Mod: BUFFER (Veritabanina Aktar butonuna basana kadar bekler)")
print("\nYAPILACAKLAR:")
print("  1. Guild -> Season -> Guild Might")
print("  2. Her kategoriye tikla + scroll yap")
print("  3. 'Veritabanina Aktar' butonuna bas\n")

try:
    sniff(filter="udp and port 5056", prn=paketi_isle, store=0)
except KeyboardInterrupt:
    print("\nTracker durduruldu.")
except Exception as e:
    print(f"Hata: {e}")
