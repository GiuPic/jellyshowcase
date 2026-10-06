# JellyShowcase

**Italiano** · [English](README.en.md)

Plugin per Jellyfin che trasforma i contenuti "scopri" di [Seerr](https://seerr.dev) (o Jellyseerr / Overseerr) in
una libreria di Jellyfin da sfogliare, così gli utenti possono trovare nuovi film e serie e richiederli da
qualsiasi client Jellyfin.

JellyShowcase è un fork di [JellyBridge](https://github.com/kinggeorges12/JellyBridge) di kinggeorges12, con
queste modifiche:

- **Le richieste restano in attesa.** Le richieste vengono create *a nome dell'utente* (`X-API-User`), quindi
  seguono i suoi permessi e i suoi limiti e aspettano l'approvazione di un amministratore (in JellyBridge
  venivano create con i permessi dell'amministratore e approvate in automatico).
- **API protette.** Tutti gli endpoint del plugin richiedono un amministratore di Jellyfin (in JellyBridge erano
  raggiungibili senza login, compresa la configurazione del plugin con l'API key di Seerr).
- **Pulsante "Richiedi"** sui titoli della libreria scopri, nel client web e nelle app basate sul web, con lo
  stato aggiornato (in attesa di approvazione, in download, disponibile). Il ❤️ preferito continua a funzionare
  come richiesta, per esempio dalle app per TV.
- **Piattaforme di streaming.** Ogni titolo riceve le piattaforme dove è davvero disponibile nella tua regione
  (in abbonamento, gratis o con pubblicità) come tag e studio, più una collezione per piattaforma
  ("Discover - Netflix", …) sfogliabile da tutti i client.
- **I titoli richiesti restano visibili** nella libreria scopri finché il contenuto vero non arriva in una
  libreria principale; poi il segnaposto viene nascosto automaticamente.

## Origine

JellyShowcase parte da **JellyBridge v4.0** di kinggeorges12 e contributori:

- Repository: https://github.com/kinggeorges12/JellyBridge
- Commit: [`d8847635`](https://github.com/kinggeorges12/JellyBridge/commit/d8847635bfa138c6762b644b44951cb86bb546f5) – "Release JellyBridge v4.0 - Compatible with Jellyfin v12.0!" (08/09/2026)
- Licenza: GNU GPL v3.0

Il primo commit di questo repository importa quel codice senza modifiche; ogni commit successivo è una modifica
di JellyShowcase, quindi `git log` mostra esattamente cosa è cambiato rispetto all'originale. La cronologia di
JellyBridge si trova nel repository originale.

## Requisiti

- Jellyfin **12.0** o successivo
- Seerr, Jellyseerr o Overseerr, con una API key
- Una cartella per la libreria scopri, leggibile e scrivibile da Jellyfin

## Installazione

1. In Jellyfin: **Dashboard → Plugin → Repository → +** e aggiungi
   `https://raw.githubusercontent.com/GiuPic/jellyshowcase/main/manifest.json`
2. Installa **JellyShowcase** dal catalogo e riavvia Jellyfin.
3. Apri la pagina del plugin, imposta URL e API key di Seerr, cartella della libreria, regione e piattaforme,
   poi crea la libreria scopri ed esegui la sincronizzazione.

Per usare il pulsante gli utenti devono esistere anche in Seerr (si importano da Jellyfin nelle impostazioni
utenti di Seerr).

## Compilazione

`./build.sh` compila il plugin con il .NET 10 SDK in Docker. `./build.sh --install` lo copia anche in un Jellyfin
locale (imposta `PLUGINS_DIR` e `CONTAINER` in un file `.build.env`). `./release.sh "note"` pubblica una Release
su GitHub e aggiorna `manifest.json`.

La documentazione delle funzioni ereditate da JellyBridge è in [README.JellyBridge.md](README.JellyBridge.md)
(in inglese).

## Licenza

GNU General Public License v3.0, come il progetto originale. Vedi [LICENSE](LICENSE).
