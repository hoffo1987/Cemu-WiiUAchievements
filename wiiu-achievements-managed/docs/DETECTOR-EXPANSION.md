# Espansione dei rilevamenti — 1 novembre 2026

Tutti i **110 obiettivi** del catalogo hanno ora una condizione eseguibile:
i **40 precedentemente non implementati** sono stati trasferiti alle regole
condivise. Sono **93 gli obiettivi sperimentali** complessivi. Questo descrive
lo stato del codice, non una certificazione di 110 sblocchi reali.
Il proprietario dispone soltanto della partita corrente: gli eventi avanzati
non sono stati riprodotti dal vivo con salvataggi di fine gioco.

Standalone e fork usano `WindWakerProgression`, gli stessi XML e lo stesso
motore. La finestra resta `0x724` byte; il pacchetto nativo resta protocollo 1,
1952 byte. La vittoria finale usa informazioni già comprese nei 26 byte di
contesto. Non sono aggiunte scritture nella memoria emulata.

ID, punti, rarità e revisione `wwhd-alpha-1` sono conservati. Non serve migrare
l'archivio: gli sblocchi e le date esistenti restano validi. Il denominatore
del completamento passa da 70 a 110; la percentuale può quindi diminuire.
Il file delle regole future rimane presente ma vuoto; solo quel complemento
può essere vuoto, mentre un set attivo vuoto continua a essere rifiutato.

## Fonti e metodo

- [Decompilazione HD, commit b2811a3](https://github.com/ZeldaWWHDDecomp/wwhd/tree/b2811a3ce838d1f875bc8e68536c1950af5d7ffc/wwhd_src):
  attori che assegnano premi, acquisizione degli oggetti, salvataggio e contesto.
  Il progetto documenta USA v0: i layout sono riferimenti tecnici, non prove
  live degli offset EUR. Non è stato incorporato il suo codice nell'app.
- [Randomizer HD, commit cb7b1c9](https://github.com/SuperDude88/TWWHD-Randomizer/tree/cb7b1c979ac031c8eae91e23fb00fc5f4d042efa):
  `logic/GameItem.hpp`, `logic/data/location_data.yaml` e patch ASM confrontate
  con il codice HD. Gli offset nei file del livello non sono indirizzi RAM.
  I flag aggiunti dal randomizer NON vengono usati come flag vanilla.
- [Mappa HD pubblicata dall'autore del trainer](https://github.com/jamesst20/ZeldaWindWakerTrainer-for-CEMU/blob/master/ZeldaWindWakerTrainer/GameOffsets.cs):
  indirizzi di inventario e carte, confrontati con le operazioni sui bit HD.
- [Autotracker Archipelago](https://github.com/ArchipelagoMW/Archipelago/tree/main/worlds/tww),
  [catalogo tecnico GC delle ricompense](https://github.com/datstarkey/ww-online/blob/1661d769036c16b0ed17f8bca8a192c4c853be52/docs/hearts.md)
  e [analisi indipendente dei premi](https://github.com/StephenSHorton/ww-multiplayer/blob/c1278b3d5aa1be80414db6c445a4428f6ef357d8/wwapi/tables/hearts/catalogue.go):
  confronti secondari di implementazioni, tenendo conto delle differenze HD.
- [Layout del salvataggio HD](https://github.com/ZeldaWWHDRecomp/ZeldaWWHDRecomp/blob/264f53ee0eca9d52d39f6ccf1a890eecec018e6e/tools/savegame/wwsave.py)
  e [checklist HD dell'autore](https://gist.github.com/chasestubblefield/10670979):
  ulteriori controlli della struttura e delle famiglie di contenuto.

L'ispezione locale, in sola lettura, dei parametri di scena dell'installazione
EUR ha inoltre identificato sei attori `Daiocta` e i premi dei due forzieri
del labirinto e della Nave Fantasma. Sono stati letti gli archivi in memoria,
senza esportare livelli, immagini, testi, RPX o altri file del gioco nel progetto.
Nei sorgenti e in questo documento rimangono solo i fatti numerici necessari.

## Condizioni dei 40 obiettivi

Gli eventi `XXYY` indicano `save[0x624 + XX] & YY == YY`. I forzieri sono bit
di un intero big-endian a `save + 0x380 + 0x24 * stage`. Le condizioni sono
condivise dalle due applicazioni. La tabella completa segue sotto.

| ID conservato | Campo / soglia | Riferimento |
| --- | --- | --- |
| `story-tower-rises` | `tower_pearls_placed` ≥ 1 | HD custom_funcs.asm / eventi delle tre statue |
| `story-medli-sage` | `medli_temple` ≥ 1 | HD eventi dei saggi 2920/2910 |
| `story-makar-sage` | `makar_temple` ≥ 1 | HD eventi dei saggi 2920/2910 |
| `story-puppet-ganon` | `puppet_ganon_defeated` ≥ 1 | d_a_bgn_exec.cpp |
| `story-ganondorf` | `ganondorf_defeated` ≥ 1 | d_a_gnd_exec.cpp / d_com_inf_game.h |
| `explore-salvage-first` | `chart_salvages` ≥ 1 | d_a_salvage.cpp / d_save_3.cpp |
| `explore-salvage-10` | `chart_salvages` ≥ 10 | d_a_salvage.cpp / d_save_3.cpp |
| `explore-salvage-46` | `chart_salvages` ≥ 46 | d_a_salvage.cpp / d_save_3.cpp |
| `explore-ghost-ship` | `ghost_ship_reward` ≥ 1 | d_a_tag_ghostship.cpp / PShip EUR |
| `explore-big-octo` | `big_octos` ≥ 1 | d_a_daiocta.cpp / sei attori EUR |
| `explore-all-octos` | `big_octos` ≥ 6 | d_a_daiocta.cpp / sei attori EUR |
| `explore-savage-30` | `savage_30` ≥ 1 | Cave09 EUR / d_a_tbox.cpp |
| `explore-savage-50` | `savage_50` ≥ 1 | Cave10 EUR / d_item_2.cpp |
| `collect-heart-piece-first` | `heart_pieces` ≥ 1 | 44 fonti distinte, tabella sotto |
| `collect-heart-pieces-12` | `heart_pieces` ≥ 12 | 44 fonti distinte, tabella sotto |
| `collect-heart-pieces-44` | `heart_pieces` ≥ 44 | 44 fonti distinte, tabella sotto |
| `collect-treasure-charts-10` | `treasure_charts` ≥ 10 | d_save_3.cpp / GameItem.hpp HD |
| `collect-treasure-charts-25` | `treasure_charts` ≥ 25 | d_save_3.cpp / GameItem.hpp HD |
| `collect-treasure-charts-46` | `treasure_charts` ≥ 46 | d_save_3.cpp / GameItem.hpp HD |
| `collect-triforce-charts` | `deciphered_triforce_charts` ≥ 3 | d_save_3.cpp / inizializzazione EA |
| `collect-tingle-statues-5` | `tingle_statues` ≥ 5 | d_a_npc_tc.cpp, isCreate |
| `collect-gallery-first` | `gallery_first` ≥ 1 | d_a_npc_mt.cpp, setFigure |
| `collect-gallery-complete` | `gallery_complete` ≥ 1 | d_a_npc_mt.cpp, isComp / _create |
| `side-killer-bees` | `killer_bees_reward` ≥ 1 | d_a_npc_mk.cpp |
| `side-sinking-ships` | `squid_win` ≥ 1 | d_a_npc_kg1.cpp / _dialogue.cpp |
| `side-sinking-ships-20` | `squid_20_reward` ≥ 1 | d_a_npc_kg1.cpp / carta 23 |
| `side-mail-25` | `mail_25` ≥ 1 | d_a_npc_bmsw.cpp, registro C203 |
| `side-orca-500` | `orca_heart_reward` ≥ 1 | d_a_npc_ji1_event.cpp |
| `side-hurricane-spin` | `hurricane_spin` ≥ 1 | d_a_npc_ji1_teachsp.cpp |
| `side-cabana` | `cabana_deed` ≥ 1 | d_a_npc_ho.cpp |
| `side-joy-pendants-40` | `teacher_chart_45` ≥ 1 | d_a_npc_ho.cpp / GameItem.hpp HD |
| `side-forest-water` | `forest_water_reward` ≥ 1 | d_a_obj_ftree.cpp / d_a_item.cpp |
| `side-mila` | `mila_reward` ≥ 1 | d_a_npc_kk1_b.cpp |
| `side-maggie` | `maggie_reward` ≥ 1 | d_a_npc_kp1.cpp, inferenza documentata |
| `side-lighthouse` | `lighthouse_reward` ≥ 1 | d_a_npc_people_msg.cpp |
| `side-flight-control` | `flight_reward` ≥ 1 | d_a_npc_bmcon1.cpp |
| `side-spectacle` | `spectacle_win` ≥ 1 | d_a_npc_kg2.cpp |
| `side-trading` | `trading_reward` ≥ 1 | d_a_npc_roten.cpp, inferenza documentata |
| `side-town-decoration` | `town_decoration_reward` ≥ 1 | d_a_npc_people_msg.cpp |
| `side-golden-feathers` | `hoskit_letter_read` ≥ 1 | d_a_obj_toripost.cpp / d_letter.cpp |

## Dettagli che evitano sblocchi errati

**Carte.** Il dispatch HD usa gli item `C2..FE`; `FF` è assente. Il bit della
carta è `FE - item`, all'interno di parole big-endian. Le maschere delle 46
carte normali a `D4..DB` sono `FF FF FF EA 00 07 FF F3`. Si escludono carte
speciali, le tre della Triforza e bit inutilizzati. Un recupero richiede sia
possesso (`D4..`) sia completamento (`F4..`), non la sola apertura (`E4..`).
La carta 45 è item `F8`, bit `D7:40`; la 46 è `F7`, bit `D7:80`.
HD inizializza a `EA` il byte delle carte decifrate `145`: quei cinque bit
non provano le tre decifrazioni ottenibili, che usano `15` insieme al possesso.

**Cuori.** Il conteggio usa **44 fonti distinte**, non la capacità massima né
il numero di boss sconfitti. La capacità serve solo a non pubblicare un totale
impossibile prima dell'applicazione dei guadagni di salute. I contenitori dei
boss non entrano nel conteggio. Composizione:

| Tipo | Fonti HD |
| --- | --- |
| 11 forzieri | stage 0: bit 10, 6, 3, 0, 20; stage 2: bit 3; stage 12: bit 26, 5, 6; stage 10: bit 1, 0 |
| 2 oggetti | stage 0, item bit 2 e 8, parola a `394` |
| 3 tesori senza carta | ocean grid 6, 16, 17, bit 0; array di u16 big-endian a `5C0` |
| 12 tesori con carta | numeri di salvataggio 9, 11, 17, 18, 13, 12, 14, 30, 15, **8**, 10, 16; possesso e recupero |
| 13 premi persistenti | eventi `0F10`, `1340`, `1B20`, `1020`, `1B10`, `2280`, `AE03`, `AC03`, `B503`, `3E04`, `2E20`, `2010`, `2B40` |
| 3 premi compositi | prima vittoria alle navi, prima vittoria ai barili, consegna a Maggie |

Il forziere stage 13 bit 12 dà l'Amuleto dell'Eroe in HD e viene escluso dai
frammenti. Il recupero alla stanza di mare 46, numero di salvataggio 8, dà
invece un frammento in HD. Queste due differenze mantengono il totale a 44.
La lista del randomizer da sola omette il frammento di Crescent Moon nella
propria etichetta della ricompensa: il parametro locale di `Salvage` conferma
item 7, numero di salvataggio 9. Non è stata copiata ciecamente una lista GC.

**Piovre Giganti.** Gli attori nelle stanze di mare 6, 17, 20, 33, 36 e 46
impostano rispettivamente gli switch `0D..12`. Si contano quei sei switch
persistenti della scena mare. Lo switch `34` della fata e i tesori recuperati
non sostituiscono l'uccisione. I parametri locali sono rispettivamente
`0DFF2802`, `0EFF2802`, `0FFF2801`, `10FF2801`, `11FF2801`, `12FF2800`;
`d_a_daiocta.cpp` legge lo switch dal byte alto e lo imposta alla sconfitta.

**Labirinto e nave.** Cave09/Cave10 usano stage 13: piano 30, bit 11,
premio `66`; piano 50, bit 12, premio `43`. L'ultimo richiede anche
l'Amuleto dell'Eroe a `B8:01`, distinto dal Talismano dei Pirati a `B7`.
PShip usa stage 10 bit 23, premio `64`; richiede anche il registro `8803=3`.
Le statue di Tingle usano il forziere 15 in ciascuno degli stage 3..7,
come la condizione HD che fa comparire Knuckle.

**Galleria e finale.** `d_a_npc_mt.cpp` considera completa la Galleria HD
con 133 dei 134 slot e imposta `3D08` entrando nella stanza; la figurina premio
viene aggiunta dopo. Il rilevatore usa quell'evento del gioco, senza importare
la soglia GC che escludeva Knuckle. `3A01` prova una figurina realizzata,
`3401` soltanto una foto consegnata. Per Puppet Ganon si usa `3F10`, impostato
dopo l'esplosione della terza forma. `3B02` e `4002` sono scene PRIMA dei boss.
Per Ganondorf, `d_a_gnd_exec.cpp`, caso finale della camera, richiede la
transizione a **GTower, spawn 2, stanza 0, layer 9**, dopo il colpo finale;
si richiedono anche Puppet Ganon, Triforza completa e spada completa.
I dati di spawn/stanza/layer vengono letti dal contesto già acquisito,
rispettivamente a `+8` (s16), `+A` e `+B` (s8).

## Limiti da verificare nel playthrough

- Forzieri e switch salvati possono aggiornarsi uscendo dalla scena. Questo
  può ritardare labirinto, statue, piovre e il conteggio dei frammenti; non
  viene introdotto un indirizzo ipotetico per la copia attiva della scena.
- Maggie usa un'inferenza strutturale: lettera di Moe posseduta in passato,
  non più nella borsa e registro `CC` azzerato. Non è un flag esclusivo del
  premio. La consegna della statua del mercante usa l'evento `3E04` del dialogo:
  va verificato il rapporto temporale con il premio. Il totale dei frammenti
  eredita questi due limiti.
- Alcuni premi impostano il flag all'avvio del dialogo o della presentazione.
  Le tre conferme non dimostrano da sole che tutta l'animazione sia terminata.
- Medli/Makar vengono riconosciuti dagli eventi della loro presenza nei
  rispettivi templi, quindi possono arrivare dopo il risveglio mostrato nel testo.
- L'obiettivo di Ganondorf richiede che l'app osservi la destinazione finale
  con Link e contesto validi per il gate e le conferme. Non è recuperabile
  dal solo possesso della Triforza o caricando un salvataggio precedente al
  duello. Finestra temporale, riavvio e menu finale restano da provare dal vivo.
- I test sintetici provano selezione dei bit, condizioni negative, passaggi
  nel motore, archivio e parità nativa; non provano la semantica completa EUR.

## Copertura del contenuto

Il catalogo copre l'intero percorso principale previsto, i boss, i sei canti,
gli strumenti principali, gli otto frammenti della Triforza e le tre carte HD,
44 frammenti di cuore, quattro bottiglie, 46 carte normali e relativi recuperi,
49 settori, sei piovre, cinque statue, entrambe le profondità del labirinto,
Nave Fantasma, Galleria e le principali secondarie/minigiochi elencati sopra.
È una copertura ampia delle famiglie principali, ancora da verificare in gioco.

Le soglie ripetute non sono attività diverse: 110 obiettivi non significa
110 missioni uniche né una percentuale misurata di tutto il gioco. Alcuni
contenuti sono coperti indirettamente dalle collezioni: ad esempio il gioco
dei maiali di Dampa tramite la carta 42. Restano senza obiettivo dedicato la
gara delle barche e la sua carta speciale, i livelli di tessera di Beedle,
tutte le decorazioni possibili, tutte le piattaforme/grotte non necessarie
alle collezioni, i record massimi ripetibili e ciascuna ricompensa in rupie.
Non viene dichiarato un «100% di tutto ciò che succede nel gioco».
