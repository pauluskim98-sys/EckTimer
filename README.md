# EckTimer

EckTimer ist eine Windows-Desktop-App. Starte EckTimer.exe per Doppelklick. Die App verwendet Windows Forms und benötigt .NET Framework 4.x.

## Bedienung

- **Timer:** Auf die große Zeitanzeige doppelklicken und die Dauer eingeben, etwa 00:05:00. Enter übernimmt die Eingabe, Escape verwirft sie. Start, Pause, Fortsetzen und Zurücksetzen beziehen sich auf die eingestellte Dauer.
- **Stoppuhr:** Start, Pause, Fortsetzen und Zurücksetzen.
- **Wecker:** Uhrzeit im 24-Stunden-Format wählen und aktivieren. Der Wecker klingelt einmal beim nächsten Erreichen der gewählten Uhrzeit. Im Wecker-Tab zeigt die große Anzeige die aktuelle Uhrzeit.
- **Mini-Fenster:** Den Fensterrand oder eine Ecke ziehen. Bei Standardskalierung sind bis zu 72 × 28 Pixel möglich. Im Mini-Modus lässt sich das Fenster an der Zeitanzeige verschieben.
- **Rechtsklick im Mini-Fenster:** Modus wechseln, das Fenster minimieren, „Immer im Vordergrund“ umschalten oder über „Bedienelemente anzeigen“ wieder vergrößern. Die Leertaste bedient im Mini-Modus den gewählten Modus.
- **Signale:** Ton und Blinken sind für Timer und Wecker separat einstellbar. Ein aktives Signal lässt sich per Escape, Klick auf die Anzeige oder „Signal stoppen“ beenden.
- **Fenster schließen:** Die App wird beendet. Beim nächsten Start lädt sie Position, Größe, Modus und Einstellungen.

## Aus dem Quellcode bauen

Das ZIP entpacken und im entpackten Ordner mit PowerShell ausführen.

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /win32manifest:EckTimer.manifest /win32icon:EckTimer.ico /out:EckTimer.exe Timekeeping.cs Notifications.cs TimerWindow.cs
```

Timekeeping.cs enthält die Zeitberechnung, Notifications.cs steuert Klingelton, Blinkphase und Taskleistenhinweis, und TimerWindow.cs enthält die Windows-Oberfläche.
- Ein Doppelklick auf die freie Fläche wechselt zwischen Mini- und Normalansicht. Beim Verkleinern startet der Countdown, beim Vergrößern pausiert er. Countdown-Dauern lassen sich per Rechtsklick unter Timer-Vorlagen speichern, laden und löschen.
- Die App startet in der hellen Ansicht. Über Einstellungen lassen sich Tagmodus, Nachtmodus und Immer im Vordergrund festlegen. Im Timer-Tab stehen die gespeicherten Dauern direkt unter den Tabs. Mit + Speichern legst du die aktuell eingestellte Dauer direkt als Vorlage ab.
