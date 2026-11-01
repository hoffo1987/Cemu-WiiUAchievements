# Percorso giocabile — build sperimentale

Questa build comprende **93 rilevamenti sperimentali**, inclusi i 40 obiettivi
implementati il 1 novembre 2026: sono eseguibili e
possono assegnare sblocchi e punti, ma devono ancora essere confermati durante
una partita reale. Il catalogo comprende **110 obiettivi attivi**, di cui 93
sperimentali, e nessun obiettivo non implementato. Fonti e limiti delle nuove
condizioni sono in [DETECTOR-EXPANSION](DETECTOR-EXPANSION.md).
Non è una certificazione del set completo.
La tunica mantiene lo stesso ID e lo sblocco già guadagnato.

## Come usarla durante la partita

Avvia `WiiUAchievements.exe` nella directory principale, collega Cemu sulla partita
già caricata e lascia attiva la registrazione mentre giochi. Non serve tornare
indietro per ogni oggetto. Le schede seguono ora un ordine indicativo di percorso;
**Tappa** / **F** seleziona il capitolo, **Categoria** / **C** il tipo di obiettivo.
I due filtri si combinano con Tutti, Sbloccati, Da sbloccare e Non attivi.
Gli extra si possono svolgere in momenti diversi: l'ordine non impone vincoli
al motore e non blocca chi esplora liberamente.

Gli obiettivi sperimentali hanno **Sperimentale** nei dettagli, `*` vicino ai punti
e una notifica indicata come sperimentale. Sono implementati. Le voci **Non
attivo**, invece, non hanno ancora una condizione e non possono sbloccarsi.

Collegati sulla partita caricata e attendi la stabilizzazione. La produzione
riconosce anche le condizioni già soddisfatte dopo il controllo del contesto
di gioco e tre conferme coerenti. Le condizioni sperimentali mantengono i loro
limiti di verifica; nessun traguardo WWHD è rimasto non attivo. Gli sblocchi
già registrati conservano le loro date.
Puoi lasciare l'app collegata durante i caricamenti: mostra **Attesa automatica**,
poi riprende dopo la stabilizzazione. Pausa/Scollega restano facoltativi nei
flussi verificati. Evidenze e casi ancora da verificare: [caricamenti automatici](AUTOMATIC-LOADS.md).

## Serie implementate

| Tappa indicativa | Rilevamenti automatici disponibili |
| --- | --- |
| 01 — Outset | Tunica già verificata; cannocchiale, spada iniziale, scudo iniziale |
| 02 — Pirati / prima fortezza | Borsa del bottino; incontro con Re Drakar dopo la prima fortezza |
| 03 — Windfall | Vela, macchina fotografica, macchina a colori, borsa delle esche |
| 04 — Isola del Drago | Wind Waker, Melodia del Vento, borsa del postino, rampino; Gohma insieme alla perla di Din |
| 05 — Bosco | Foglia Deku, boomerang; Kalle Demos insieme alla perla di Farore |
| 06 — Jabun / ritorno a casa | Bombe, perla di Nayru; nonna guarita insieme alla zuppa ricevuta |
| 07 — Torre degli Dei | Melodia del Burattino, arco, Gohdan |
| 08 — Hyrule | Prima Spada Suprema, Melodia del Ciclone |
| 09 — Ritorno alla fortezza | Martello, Helmaroc King, rivelazione di Zelda |
| 10 — Terra | Frecce elementali, bracciali, stivali di ferro, melodia della terra, scudo specchio; Jalhalla insieme al ripristino della spada |
| 11 — Vento | Melodia del vento del tempio, arpione; Molgera insieme al pieno potere della spada |
| 12 — Triforza | Primo frammento; tutti gli otto frammenti |
| 13 — Finale | Barriera abbattuta; frecce di luce; Puppet Ganon; transizione finale dopo Ganondorf |
| 14 — Extra liberi | Vela veloce, armatura magica, magia doppia, capacità bombe/frecce 99, portafoglio 5000, canzone del giorno/notte, sei melodie, quattro bottiglie, carta nautica 10/25/49 settori |
| 15 — Crescita | Le 16 soglie già presenti su rupie e capacità dei cuori |

I boss usano il completamento del dungeon memorizzato, non l'aumento dei cuori.
Il salvataggio del relativo bit può aggiornarsi uscendo dall'area del boss o
usando il portale: una notifica può quindi arrivare dopo il combattimento.
Le perle di Din/Farore e i ripristini della spada sono condizioni aggiuntive,
senza modificare il significato degli ID esistenti.

## I 40 rilevamenti aggiunti e i limiti

Sono ora eseguibili anche tutti i minigiochi e premi del catalogo, Galleria,
tesori sommersi, carte, statue, frammenti di cuore e gli eventi di storia/finale
prima mancanti. La tabella completa dei 40 ID e i limiti specifici sono in
[DETECTOR-EXPANSION](DETECTOR-EXPANSION.md). Alcuni flag salvati si aggiornano
dopo l'uscita dalla scena; Maggie/scambi usano inferenze ancora da verificare.
Il finale di Ganondorf deve essere osservato mentre avviene: tenere aperta la
registrazione durante il duello e la scena successiva.

Se durante il gioco un evento implementato non si sblocca, bastano il nome
dell'obiettivo, l'azione appena fatta e lo stato mostrato nella scheda/registro.
Non serve ricaricare il salvataggio subito. Il registro distingue le notifiche
`[TEST]`; i progressi restano locali. Le osservazioni non promuovono da sole una
mappatura a verificata.

## Fonti e limiti

Gli indirizzi HD di inventario e capacità derivano dalla ricerca pubblica del
[trainer di riferimento](https://github.com/jamesst20/ZeldaWindWakerTrainer-for-CEMU/blob/master/ZeldaWindWakerTrainer/GameOffsets.cs).
I flag di storia e il layout dei dungeon sono ipotesi ricavate dalle definizioni
pubbliche di [save layout](https://github.com/zeldaret/tww/blob/main/include/d/d_save.h),
[item acquisition](https://github.com/zeldaret/tww/blob/main/src/d/d_item.cpp),
[event flags](https://github.com/zeldaret/tww/blob/main/include/d/d_save_event_flag.inc)
e [indici di inventario/simboli](https://github.com/zeldaret/tww/blob/main/include/d/d_com_inf_game.h).
Il decoder C# è indipendente; nessun codice di gioco è incorporato.

I test ricostruiscono dati sintetici e attraversano il vero parser, motore e
salvataggio, ma non dimostrano che ogni indirizzo della versione HD sia corretto.
Una lettura passiva sulla sessione EUR iniziale ha prodotto 50 campi disponibili,
compatibili con la tunica, il portafoglio iniziale e tre settori iniziali. Non
prova le acquisizioni future. Stato delle prove: `VALIDATION.md`, dettagli tecnici:
`MEMORY-MAP.md`. Riavvii reali di Cemu e la partita completa restano da verificare.

Titoli, nomi italiani, soglie descritte e icone sono stati ricontrollati il
31 ottobre 2026: [revisione completa delle 110 voci](ACHIEVEMENT-AUDIT.md).
Il file editoriale è la fonte dei testi attualmente mostrati dall'app; le tabelle
di ricerca qui sopra possono usare anche i nomi inglesi, tuttora ricercabili.
