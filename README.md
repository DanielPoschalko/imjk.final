# IMJK Filmportal

ASP.NET-Core-8-Filmportal mit SQLite, sicherer Cookie-Anmeldung, Watchlist, Bewertungen, Empfehlungen, Filmlisten, Moderation und automatischem TMDB-Import.

## Projektstruktur

```text
src/IMJK/          Anwendung und Weboberfläche
  Api/             HTTP-Endpunkte nach Fachbereich
  Contracts/       Request-Modelle
  Data/            Datenbank und Initialisierung
  Domain/          Persistente Fachmodelle
  Services/        TMDB-Client und Importlogik
  wwwroot/         HTML-, CSS- und JavaScript-Dateien
  seed/            Startkatalog für eine leere Datenbank
  app-data/        Lokale SQLite-Daten und Startdaten

tests/IMJK.Tests/  Datenbank-, Neustart-, Import- und API-Tests
IMJK.sln           Solution mit Anwendung und Tests
```

## Website verwenden

Nach `dotnet run` zeigt das Terminal die lokale Adresse an, normalerweise `http://localhost:5000`. Öffne sie im Browser.

- **Entdecken:** Filme suchen und nach Genre, Darsteller, Jahr oder Bewertung filtern und sortieren.
- **Anmelden:** Mit „Registrieren“ ein lokales Konto erstellen; danach stehen persönliche Funktionen bereit.
- **Merkliste und Gesehen:** Die entsprechenden Schaltflächen auf einer Filmkarte oder in den Details umschalten.
- **Bewerten:** „Details“ öffnen, 1 bis 10 Punkte und optional eine Rezension eingeben.
- **Community:** Rezensionen als hilfreich markieren oder kommentieren.
- **Für dich:** Empfehlungen aus Lieblingsgenres und gut bewerteten Filmen anzeigen; gesehene Filme werden ausgelassen.
- **Listen:** Öffentliche oder private Filmlisten anlegen, Filme hinzufügen und andere registrierte Nutzer einladen.
- **Profil:** Benutzernamen und Lieblingsgenres bearbeiten.
- **Film vorschlagen:** Angemeldete Nutzer reichen einen Film ein; normale Vorschläge warten auf die Admin-Freigabe.
- **Admin:** Vorgeschlagene Filme freigeben oder ablehnen und Rezensionen moderieren.

## So arbeitet das Programm

`Program.cs` baut die ASP.NET-Core-Anwendung auf. Beim Start wird zuerst die SQLite-Datenbank angelegt oder erweitert. Danach läuft der TMDB-Import. Erst anschließend beginnt der Webserver, statische Dateien und API-Endpunkte auszuliefern.

Der Browser lädt `wwwroot/index.html`, `styles.css` und `app.js`. `app.js` ruft die JSON-Endpunkte unter `/api` auf, aktualisiert die Filmkarten und öffnet Formulare als Dialoge. Die Oberfläche greift nie direkt auf SQLite oder auf den TMDB-Token zu.

Die Endpunkte sind nach Aufgaben getrennt:

- `Api/AuthEndpoints.cs`: Registrierung, Login, Logout und Profil
- `Api/MovieEndpoints.cs`: Filmkatalog, Filter, Details, Bewertungen und TMDB-Status
- `Api/CommunityEndpoints.cs`: Watchlist, gesehen, Empfehlungen, Kommentare und Listen
- `Api/AdminEndpoints.cs`: Freigabe, Ablehnung und Moderation

`Services/TmdbImportService.cs` fragt TMDB ab. Es lädt deutsche Kinostarts für Deutschland, danach für jeden Film Details und Credits. Bekannte TMDB-IDs werden aktualisiert; neue IDs werden angelegt. Der Zeitpunkt eines erfolgreichen Imports wird erst gespeichert, nachdem der gesamte Lauf beendet ist. Bei einem Fehler bleiben Daten und alter Zeitpunkt erhalten, damit der Zeitraum beim nächsten Start erneut versucht wird.

## Speicherung

Alle dauerhaften Anwendungsdaten liegen standardmäßig in `app-data/filmportal.db`:

- `Movies`: Filmdaten, TMDB-ID, Erscheinungsdatum, Poster-URL und Importquelle
- `Users`: Benutzername, gehashtes Passwort, Rolle und Lieblingsgenres
- `Reviews`, `Comments`, `HelpfulVotes`: Bewertungen und Community-Daten
- `UserMovies`: Watchlist- und Gesehen-Status pro Nutzer
- `Lists`, `ListItems`, `ListContributors`: persönliche und gemeinsam bearbeitete Listen
- `Settings`: Zeitpunkt des letzten erfolgreichen TMDB-Imports

Beim Neustart wird dieselbe SQLite-Datei geöffnet. Deshalb bleiben Konten, Bewertungen, Listen und persönliche Filmzustände erhalten. Mit `DATA_DIR=/anderer/pfad` kann ein anderer Speicherort verwendet werden.

Der TMDB-Token liegt im lokalen .NET-Secret-Speicher des Betriebssystems und nicht in `filmportal.db` oder Git. Die Film-Poster werden nicht lokal kopiert; gespeichert wird die von TMDB gelieferte Bild-URL.

## Lokal starten

```bash
dotnet restore
dotnet run
```

Die SQLite-Datei liegt unter `app-data/filmportal.db`. `DATA_DIR` kann einen anderen Datenordner festlegen.

## Automatischer TMDB-Import

Für den Import wird ein persönlicher TMDB API Read Access Token empfohlen. Er ist in den TMDB-Kontoeinstellungen unter **API** erhältlich. Das Token bleibt ausschließlich auf dem Server und gehört nicht in Git.

```bash
dotnet user-secrets set TMDB_READ_ACCESS_TOKEN 'dein-read-access-token'
dotnet run
```

Die User Secrets werden außerhalb des Repositorys im Benutzerprofil gespeichert und nicht von Git erfasst. Alternativ wird `TMDB_API_KEY` als Umgebungsvariable oder User Secret unterstützt. Bei jedem Start fragt die App deutsche Kinostarts vom letzten erfolgreichen Import bis zum aktuellen Tag ab. Beim allerersten Import werden standardmäßig die letzten 30 Tage geladen. Für jeden neuen Film werden deutsche Beschreibung, Startdatum, Genres, Regie, Besetzung, TMDB-Wertung und Poster gespeichert. Bereits bekannte Filme werden aktualisiert und nicht dupliziert.

Optionale Einstellungen:

- `TMDB_REGION=DE` – Land für Kinostarts
- `TMDB_LANGUAGE=de-DE` – Sprache der Metadaten
- `TMDB_INITIAL_LOOKBACK_DAYS=30` – Zeitraum beim ersten Import

Wenn Zugangsdaten fehlen oder TMDB vorübergehend nicht erreichbar ist, startet die Website trotzdem mit den gespeicherten Daten. Ein fehlgeschlagener Lauf verschiebt den gespeicherten Zeitpunkt nicht und wird beim nächsten Start wiederholt.

Die App zeigt die vorgeschriebene TMDB-Quellenangabe im Footer. Dieses Projekt setzt eine nicht kommerzielle Nutzung voraus; für eine kommerzielle Nutzung gelten die TMDB-Lizenzbedingungen.

## Admin-Konto

Vor dem ersten Start kann ein Admin-Konto angelegt werden:

```bash
ADMIN_USERNAME=siteadmin ADMIN_PASSWORD='ein-eigenes-langes-passwort' dotnet run
```

Das Passwort muss mindestens zwölf Zeichen haben und wird nur gehasht gespeichert.

## Bestehende Daten

Beim ersten Start importiert die App vorhandene JSON-Daten nach SQLite und hasht importierte Nutzerpasswörter. Das unsichere frühere Demo-Konto `admin` mit Passwort `1234` wird nicht übernommen. Bestehende SQLite-Datenbanken werden beim Start um die TMDB-Felder und den Importstatus erweitert, ohne vorhandene Filme oder Nutzerdaten zu löschen.

## Tests

```bash
dotnet test IMJK.sln
```

Die Tests prüfen unter anderem Passwortmigration, Datenpersistenz nach einem Neustart, Schema-Aktualisierung, TMDB-Daten und Bilder, Dublettenvermeidung, den Zeitraum ab dem letzten erfolgreichen Start sowie das Verhalten bei API-Ausfällen.
