; ============================================================
;  Albion Online At Arayan Kelebekler Tools - Inno Setup Script
;  Kullanim: build.ps1 tarafindan otomatik cagrilir
;            veya elle: ISCC.exe setup.iss
; ============================================================

#define MyAppName      "Albion Online At Arayan Kelebekler Tools"
#define MyAppShortName "AlbionAAK"
#define MyAppPublisher "At Arayan Kelebekler"
#define MyAppExeName   "StatisticsAnalysisTool.exe"
#define MyAppURL       "https://github.com/srknsrt/AlbionOnline-AAK"

; Bu degiskenler build.ps1 tarafindan /D parametresiyle aktarilir
; Dogrudan ISCC cagrisinda da verilebilir:
;   ISCC.exe setup.iss /DMyAppVersion=8.6.9.0 /DPublishDir=C:\...\bin\Publish /DOutputDir=C:\...\installer\Output
#ifndef MyAppVersion
  #define MyAppVersion "8.6.9.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\bin\Publish"
#endif
#ifndef OutputDir
  #define OutputDir "Output"
#endif

; ── Temel ayarlar ────────────────────────────────────────────
[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename=AlbionAAK-v{#MyAppVersion}-Setup
SetupIconFile=..\src\StatisticsAnalysisTool\Assets\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
MinVersion=10.0
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no
ShowLanguageDialog=auto

; ── Diller ───────────────────────────────────────────────────
[Languages]
Name: "turkish";  MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english";  MessagesFile: "compiler:Default.isl"
Name: "german";   MessagesFile: "compiler:Languages\German.isl"

; ── Ozel mesajlar ────────────────────────────────────────────
[CustomMessages]
turkish.DotNetMissing=.NET 9.0 Desktop Runtime bulunamadi. Internet'ten indirilip kurulacaktir. Devam etmek istiyor musunuz?
turkish.NpcapMissing=Npcap ag surucusu bulunamadi. Ag izleme ozelligi icin gereklidir. Simdi kurmak istiyor musunuz?
turkish.InstallingDotNet=.NET 9.0 Desktop Runtime kuruluyor...
turkish.InstallingNpcap=Npcap kuruluyor...
english.DotNetMissing=.NET 9.0 Desktop Runtime not found. It will be downloaded and installed from the internet. Do you want to continue?
english.NpcapMissing=Npcap network driver not found. It is required for network monitoring features. Would you like to install it now?
english.InstallingDotNet=Installing .NET 9.0 Desktop Runtime...
english.InstallingNpcap=Installing Npcap...

; ── Dosyalar ─────────────────────────────────────────────────
[Files]
; Ana uygulama dosyasi
Source: "{#PublishDir}\{#MyAppExeName}";         DestDir: "{app}"; Flags: ignoreversion

; JSON veri dosyalari
Source: "{#PublishDir}\*.json";                  DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; XML konfigürasyon dosyalari
Source: "{#PublishDir}\*.xml";                   DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Resources klasoru (oyun ikonu, gorsel veriler)
Source: "{#PublishDir}\Resources\*";             DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Assets klasoru (ikonlar, resimler)
Source: "{#PublishDir}\Assets\*";                DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Ses dosyalari
Source: "{#PublishDir}\Sounds\*";                DestDir: "{app}\Sounds"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Lokalizasyon dosyalari (uygulama dil dosyasi)
Source: "{#PublishDir}\Localization\*";          DestDir: "{app}\Localization"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Installer dil dosyalari
Source: "{#PublishDir}\Languages\*";             DestDir: "{app}\Languages"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Diger DLL / runtime dosyalari
Source: "{#PublishDir}\*.dll";                   DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#PublishDir}\*.pdb";                   DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; WebView2 native runtime
Source: "{#PublishDir}\runtimes\*";              DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Avalon Roads verileri (harita resimleri ve JSON)
Source: "{#PublishDir}\Avalon\*";                DestDir: "{app}\Avalon"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Oyun veri dosyalari
Source: "{#PublishDir}\GameFiles\*";             DestDir: "{app}\GameFiles"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Gorsel kaynaklar
Source: "{#PublishDir}\ImageResources\*";        DestDir: "{app}\ImageResources"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Kelebek Tracker (lokal - GitHub'a dahil edilmez)
Source: "{#PublishDir}\Trackers\*";              DestDir: "{app}\Trackers"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Npcap installer (dependencies klasorune manuel eklenmelidir)
Source: "dependencies\npcap-installer.exe";      DestDir: "{tmp}"; Flags: ignoreversion skipifsourcedoesntexist

; ── Kisayollar ───────────────────────────────────────────────
[Icons]
Name: "{group}\{#MyAppName}";         Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Kaldir";               Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";   Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

; ── Gorevler ─────────────────────────────────────────────────
[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek seçenekler:"

; ── Kurulum sonrasi calistir ──────────────────────────────────
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Uygulamayı şimdi başlat"; Flags: nowait postinstall skipifsilent shellexec

; ── Kod bolumu: Onkosul kontrolleri ─────────────────────────
[Code]

// .NET 9.0 Desktop Runtime kurulu mu kontrol et
function IsDotNet9Installed(): Boolean;
var
  ResultCode: Integer;
  Key: String;
begin
  // Kayit defterinde .NET 9 Desktop Runtime ara
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  Result := RegKeyExists(HKLM, Key) or RegKeyExists(HKCU, Key);

  // Alternatif: dotnet --list-runtimes ile kontrol
  if not Result then begin
    if Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Result := (ResultCode = 0);
  end;
end;

// Npcap kurulu mu kontrol et
function IsNpcapInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Npcap') or
            RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Npcap') or
            FileExists(ExpandConstant('{sys}\Npcap\wpcap.dll'));
end;

// WinHttp ile dosya indir
function idpDownloadFile(URL, Filename: String): Boolean;
var
  WinHttpReq: Variant;
  FileStream: TFileStream;
  Buffer: AnsiString;
begin
  Result := False;
  try
    WinHttpReq := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    WinHttpReq.Open('GET', URL, False);
    WinHttpReq.Send('');
    if WinHttpReq.Status = 200 then begin
      Buffer := WinHttpReq.ResponseBody;
      FileStream := TFileStream.Create(Filename, fmCreate);
      try
        FileStream.WriteBuffer(Buffer[1], Length(Buffer));
        Result := True;
      finally
        FileStream.Free;
      end;
    end;
  except
    Result := False;
  end;
end;

// .NET 9 web'den indir ve kur
procedure InstallDotNet9();
var
  ResultCode: Integer;
  TempFile: String;
  DownloadURL: String;
begin
  DownloadURL := 'https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe';
  TempFile := ExpandConstant('{tmp}\dotnet9-runtime.exe');

  WizardForm.Hide;

  if not idpDownloadFile(DownloadURL, TempFile) then begin
    MsgBox('.NET 9.0 indirilemedi. Lutfen https://dotnet.microsoft.com/download/dotnet/9.0 adresinden manuel kurunuz.', mbError, MB_OK);
    WizardForm.Show;
    Exit;
  end;

  if Exec(TempFile, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then begin
    if ResultCode <> 0 then
      MsgBox('.NET 9.0 kurulumu tamamlanamadi. Lutfen manuel olarak kurunuz.', mbError, MB_OK);
  end;

  WizardForm.Show;
end;

// Npcap kur (dependencies klasorunden)
procedure InstallNpcap();
var
  ResultCode: Integer;
  NpcapInstaller: String;
begin
  NpcapInstaller := ExpandConstant('{tmp}\npcap-installer.exe');
  if FileExists(NpcapInstaller) then begin
    Exec(NpcapInstaller, '/S', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
  end else begin
    MsgBox('Npcap installer bulunamadi. Lutfen https://npcap.com/#download adresinden manuel kurunuz.', mbInformation, MB_OK);
  end;
end;

// Kurulum oncesi kontroller
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  // .NET 9 kontrolu
  if not IsDotNet9Installed() then begin
    if MsgBox(CustomMessage('DotNetMissing'), mbConfirmation, MB_YESNO) = IDYES then
      InstallDotNet9()
    else begin
      Result := '.NET 9.0 Desktop Runtime kurulmadan devam edilemiyor.';
      Exit;
    end;
  end;

  // Npcap kontrolu
  if not IsNpcapInstalled() then begin
    if MsgBox(CustomMessage('NpcapMissing'), mbConfirmation, MB_YESNO) = IDYES then
      InstallNpcap();
    // Npcap zorunlu degil, devam edilebilir
  end;
end;

