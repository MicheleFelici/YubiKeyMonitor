# YubiKeyMonitor

**YubiKeyMonitor** è un’applicazione WPF in C# per sistemi Windows che monitora l’inserimento, la rimozione e l’utilizzo delle **YubiKey**. Avviato in background con un’icona nella System Tray, fornisce un overlay grafico in primo piano ogni volta che viene rilevata un’azione della YubiKey (inserimento/rimozione o pressione del tasto).

> Sviluppato da [**Michele Felici**](https://github.com/MicheleFelici)

---

## Caratteristiche Principali

1. **Rilevamento Plug & Play**  
   - Utilizza WMI (ManagementEventWatcher) per intercettare in tempo reale l’inserimento (EventType 2) e la rimozione (EventType 3) di dispositivi USB YubiKey.

2. **Feedback sull’Input**  
   - Sfrutta Raw Input (Win32) per rilevare quando la YubiKey invia un segnale (ad esempio, un OTP).
   - Mostra un’animazione (finestra in overlay) per evidenziare chiaramente l’utilizzo della chiave.

3. **Overlay Sottile e Trasparente**  
   - Finestra WPF senza bordi (WindowStyle=None), semitrasparente (Opacity=0.8) e sempre in primo piano (Topmost).
   - Posizionata nell’angolo in basso a destra dello schermo, occupa pochissimo spazio e non disturba l’utente.

4. **Icona nella System Tray**  
   - Niente barra delle applicazioni: l’app rimane minimizzata nell’area di notifica (accanto all’orologio).
   - Dal menu contestuale (clic destro) è possibile chiudere l’applicazione o gestire altre opzioni.

5. **Avvio Automatico Opzionale**  
   - All’avvio dell’app, se non esiste già, viene creata la chiave di registro `YubiKeyMonitorWPF` in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` per l’avvio automatico a login dell’utente.

---

## Requisiti

- **Windows 7 SP1** o successivo (testato su Windows 10 e Windows 11).
- **.NET 7** o superiore (oppure .NET 8 se adattato di conseguenza).
- Permessi per scrivere nel registro di sistema (opzionale, se si vuole l’avvio automatico).
- Una **YubiKey** (serie 4, 5, NEO, …) con Vendor ID `1050`.

---

## Come Compilare

1. **Clona** o **scarica** questa repository.
2. Apri la soluzione (`.sln`) con **Visual Studio** o un editor compatibile.
3. Seleziona la configurazione desiderata (`Debug` o `Release`).
4. Compila la soluzione: l’eseguibile finale sarà in `bin\Release` (o `bin\Debug`).

---

## Come Utilizzare

1. **Esegui** il file `YubiKeyMonitorWPF.exe`.
2. Apparirà nell’angolo in basso a destra dello schermo una piccola finestra sovrapposta con l’icona della YubiKey.
3. Nell’area di notifica (tray) vedrai l’icona di YubiKeyMonitor.
   - Clic destro → *Esci* per terminare l’app.
4. **Inserisci** la tua YubiKey in una porta USB.
   - L’app la rileverà e potrai vederne la presenza sull’overlay.
5. **Premi il pulsante** della YubiKey per inviare OTP o altro.
   - L’overlay mostrerà un effetto visivo (es. lampeggio / cambio colore).

L’app, se autorizzata, si imposta automaticamente in **avvio all’accensione**. In caso non volessi, puoi rimuovere la chiave “YubiKeyMonitorWPF” da `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

---

## Personalizzazione

- **Posizione**: Il metodo `InitializePosition()` in `MainWindow.xaml.cs` determina dove appare la finestra (di default in basso a destra).  
- **Animazione**: Il metodo `TriggerBackgroundAnimation()` gestisce la logica grafica (colore, durata, ecc.).  
- **Opacità**: In XAML, `Opacity="0.8"` regola la trasparenza dell’overlay.
