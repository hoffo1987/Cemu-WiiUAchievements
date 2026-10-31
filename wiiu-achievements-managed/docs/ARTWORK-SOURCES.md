# Wind Waker medal artwork sources

## Scope and provenance

On 2026-10-31 the owner explicitly requested internet-sourced Wind Waker PNGs
and precise non-generative compositing inside supplied rarity frames. The first
batch filled 57 missing icons. A subsequent complete editorial review replaced
the Canzone del Vento image because its baked-in wording was incorrect, refined
the three sea-sector icons, made all four bottles visible and corrected the
first barrel-game reward to a Piece of Heart rather than a treasure map. There are now
58 reproducible compositions and 52 byte-identical earlier owner icons.
The superseded owner image's original hash remains in the manifest.

Nintendo promotional artwork and official item renders are distinguished from
published PNGs of original game models. The latter are community-hosted image
renders, not publisher promotional artwork. Only public PNGs were downloaded;
no game, model archive, ROM, keys or local dump was accessed. Source imagery
remains the property of its respective owners; this register grants no licence.
See [the 110-objective review](ACHIEVEMENT-AUDIT.md).

## Reproduce the exact composition

The developer tool requires Python 3 and Pillow. Neither is a runtime dependency.
Run from the repository root:

```powershell
python -I tools/compose-achievement-icons.py fetch
python -I tools/compose-achievement-icons.py render
python -I tools/compose-achievement-icons.py install
```

The [composition manifest](../tools/artwork/wind-waker-hd/compositions.json)
records URLs, SHA-256 hashes, subjects, layer positions, rotations, rarities,
template hashes and the hashes of the 52 preserved runtime PNGs. Templates are
unchanged copies of the owner's files from the supplied Mobile Devices folder.
The sky reference is also owner-supplied. The original foreground frame/boat is
placed over each proportionally resized subject. Two blurred silhouette shadows
provide depth; only threshold medals add a small code-drawn Nunito number badge.
The Kreeb PNG's bright green background is removed with a narrow chroma key.
No subject, face, character, frame or texture is generated or repainted.

Reviewed full-resolution 1254×1254 PNGs and three labelled contact sheets are
exported to ignored `output/achievement-icons/`; runtime PNGs are 256×256.
Public source caches stay in ignored `test-results/art-sources/`. Fetch validates
source hashes and refuses changed images; install checks current editorial
rarities and preserved owner hashes. For a local refinement, `render --only <id>`
updates one composition while retaining the other exports.

## Achievement mapping

| Achievement ID | Title | Rarity | Sources |
| --- | --- | --- | --- |
| `side-killer-bees` | Trovati tutti! | bronze | [bees](https://zeldawiki.wiki/wiki/File:TWW_Killer_Bees_Artwork.png) |
| `side-sinking-ships` | Centro e affondato | bronze | [salvatore](https://zeldawiki.wiki/wiki/File:TWW_Salvatore_Artwork.png) |
| `side-sinking-ships-20` | Una mira da venti colpi | silver | [chart](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png), [salvatore](https://zeldawiki.wiki/wiki/File:TWW_Salvatore_Artwork.png) |
| `collect-winds-requiem` | Canzone del Vento | bronze | [zephos](https://zeldawiki.wiki/wiki/File:TWW_Zephos_Artwork.png) |
| `side-mail-25` | Postino provetto | silver | [note](https://zeldawiki.wiki/wiki/File:TWWHD_Note_to_Mom_Artwork.png), [baito](https://zeldawiki.wiki/wiki/File:TWW_Baito_Model.png) |
| `story-tower-rises` | Tre perle, una torre | silver | [pearls-din](https://zeldawiki.wiki/wiki/File:TWWHD_Din%27s_Pearl_Artwork.png), [pearls-farore](https://zeldawiki.wiki/wiki/File:TWWHD_Farore%27s_Pearl_Artwork.png), [pearls-nayru](https://zeldawiki.wiki/wiki/File:TWWHD_Nayru%27s_Pearl_Artwork.png) |
| `collect-command-melody` | Canto del Burattino | bronze | [link-baton](https://zeldawiki.wiki/wiki/File:TWW_Link_Wind_Waker_Artwork.png) |
| `story-gohdan` | Il giudizio di Kuznar | silver | [gohdan](https://zeldawiki.wiki/wiki/File:TWW_Gohdan_Artwork.png) |
| `collect-ballad-gales` | Melodia del Tifone | bronze | [cyclos](https://zeldawiki.wiki/wiki/File:TWW_Cyclos_Artwork.png) |
| `story-helmaroc` | Mai più in gabbia | silver | [helmaroc](https://zeldawiki.wiki/wiki/File:TWW_Helmaroc_King_Artwork.png) |
| `collect-earth-lyric` | Inno della Terra | bronze | [laruto](https://zeldawiki.wiki/wiki/File:TWW_Laruto_Artwork.png) |
| `story-medli-sage` | La saggia della terra | silver | [medli](https://zeldawiki.wiki/wiki/File:TWWHD_Medli_Artwork.png) |
| `story-jalhalla` | Luce nelle tenebre | silver | [jalhalla](https://zeldawiki.wiki/wiki/File:TWW_Jalhalla_Artwork.png) |
| `collect-wind-aria` | Inno del Vento | bronze | [fado](https://zeldawiki.wiki/wiki/File:TWW_Fado_Artwork.png) |
| `story-makar-sage` | Il saggio del vento | silver | [makar](https://zeldawiki.wiki/wiki/File:TWWHD_Makar_Artwork.png) |
| `story-molgera` | Il vento torna a cantare | gold | [molgera](https://zeldawiki.wiki/wiki/File:TWW_Molgera_Artwork.png) |
| `story-triforce-first` | Un frammento di coraggio | bronze | [shards](https://zelda.nintendo.com/about/) |
| `story-triforce-complete` | Otto frammenti di coraggio | gold | [fragment-1](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_1_%28TWW%29.png), [fragment-2](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_2_%28TWW%29.png), [fragment-3](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_3_%28TWW%29.png), [fragment-4](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_4_%28TWW%29.png), [fragment-5](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_5_%28TWW%29.png), [fragment-6](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_6_%28TWW%29.png), [fragment-7](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_7_%28TWW%29.png), [fragment-8](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_8_%28TWW%29.png) |
| `story-hyrule-barrier` | Oltre l'ultima barriera | bronze | [sword](https://zeldawiki.wiki/wiki/File:TWWHD_Master_Sword_Artwork.png) |
| `story-puppet-ganon` | I fili di Triganon | silver | [puppet](https://zeldawiki.wiki/wiki/File:TWW_Puppet_Ganon_Artwork.png) |
| `story-ganondorf` | Un nuovo orizzonte | gold | [ganondorf](https://zeldawiki.wiki/wiki/File:TWWHD_Ganondorf_Artwork.png) |
| `explore-big-octo` | Occhi sull'abisso | bronze | [big-octo](https://zeldawiki.wiki/wiki/File:TWW_Big_Octo_Model.png) |
| `explore-all-octos` | Un mare senza Octoboss | silver | [big-octo](https://zeldawiki.wiki/wiki/File:TWW_Big_Octo_Model.png) |
| `explore-chart-10` | Dieci settori di mare | bronze | [fishman](https://zeldawiki.wiki/wiki/File:TWWHD_Fishman_Artwork.png) |
| `explore-chart-25` | Metà del Grande Mare | silver | [fishman](https://zeldawiki.wiki/wiki/File:TWWHD_Fishman_Artwork.png) |
| `explore-chart-49` | Il mare non ha segreti | gold | [fishman](https://zeldawiki.wiki/wiki/File:TWWHD_Fishman_Artwork.png) |
| `explore-salvage-first` | Il primo tesoro sommerso | bronze | [sunken-chest](https://zeldawiki.wiki/wiki/File:TWW_Sunken_Chest_Model.png) |
| `explore-salvage-10` | Dieci tesori sommersi | silver | [chart](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png), [sunken-chest](https://zeldawiki.wiki/wiki/File:TWW_Sunken_Chest_Model.png) |
| `explore-salvage-46` | Tutti i tesori sommersi | gold | [chart](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png), [sunken-chest](https://zeldawiki.wiki/wiki/File:TWW_Sunken_Chest_Model.png) |
| `explore-ghost-ship` | A bordo del mistero | silver | [ghost-chart](https://zeldawiki.wiki/wiki/File:TWWHD_Ghost_Ship_Chart_Artwork.png), [ghost-ship](https://zeldawiki.wiki/wiki/File:TWW_Ghost_Ship_Model.png) |
| `explore-savage-30` | Il tesoro del piano 30 | silver | [sunken-chest](https://zeldawiki.wiki/wiki/File:TWW_Sunken_Chest_Model.png), [shards](https://zelda.nintendo.com/about/) |
| `explore-savage-50` | Il fondo del labirinto | gold | [charm](https://zeldawiki.wiki/wiki/File:TWWHD_Hero%27s_Charm_Artwork.png) |
| `collect-heart-piece-first` | Un quarto di speranza | bronze | [heart-piece](https://www.zeldadungeon.net/wiki/File:TWW-Piece-of-Heart.png) |
| `collect-heart-pieces-12` | Dodici frammenti di cuore | silver | [heart-piece](https://www.zeldadungeon.net/wiki/File:TWW-Piece-of-Heart.png) |
| `collect-heart-pieces-44` | Ogni frammento al suo posto | gold | [heart-piece](https://www.zeldadungeon.net/wiki/File:TWW-Piece-of-Heart.png) |
| `collect-treasure-charts-10` | Dieci Mappe del Tesoro | bronze | [chart](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png) |
| `collect-treasure-charts-25` | Venticinque mappe | silver | [chart](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png) |
| `collect-treasure-charts-46` | Tutte le Mappe del Tesoro | gold | [chart](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png) |
| `collect-triforce-charts` | Tre mappe da decifrare | silver | [triforce-chart](https://zeldawiki.wiki/wiki/File:TWWHD_Triforce_Chart_Artwork.png) |
| `collect-tingle-statues-5` | Le cinque statue di Tingle | silver | [statue-dragon](https://zeldawiki.wiki/wiki/File:TWW_Dragon_Tingle_Statue_Model.png), [statue-forbidden](https://zeldawiki.wiki/wiki/File:TWW_Forbidden_Tingle_Statue_Model.png), [statue-goddess](https://zeldawiki.wiki/wiki/File:TWW_Goddess_Tingle_Statue_Model.png), [statue-earth](https://zeldawiki.wiki/wiki/File:TWW_Earth_Tingle_Statue_Model.png), [statue-wind](https://zeldawiki.wiki/wiki/File:TWW_Wind_Tingle_Statue_Model.png) |
| `collect-gallery-first` | La prima miniatura | bronze | [gallery-figurine](https://zeldawiki.wiki/wiki/File:TWW_Fado_Figurine_Model.png) |
| `collect-gallery-complete` | Un mare in miniatura | gold | [fig-medli](https://zeldawiki.wiki/wiki/File:TWW_Medli_Figurine_Model.png), [fig-makar](https://zeldawiki.wiki/wiki/File:TWW_Makar_Figurine_Model.png), [fig-ganondorf](https://zeldawiki.wiki/wiki/File:TWW_Ganondorf_Figurine_Model.png) |
| `collect-bottles-4` | Quattro ampolle | silver | [bottle](https://zeldawiki.wiki/wiki/File:TWWHD_Empty_Bottle_Artwork.png) |
| `collect-song-passing` | Dal giorno alla notte | bronze | [tott](https://zeldawiki.wiki/wiki/File:TWW_Tott_Artwork.png) |
| `collect-songs-6` | Le sei melodie | silver | [baton](https://zeldawiki.wiki/wiki/File:TWWHD_Wind_Waker_Artwork.png) |
| `side-trading` | L'ultimo scambio | silver | [merchant](https://zeldawiki.wiki/wiki/File:TWW_Wandering_Merchant_Artwork.png), [guru-statue](https://zeldawiki.wiki/wiki/File:TWW_Shop_Guru_Statue_Render.png) |
| `side-spectacle` | Artiglieria di precisione | bronze | [salvatore](https://zeldawiki.wiki/wiki/File:TWW_Salvatore_Artwork.png), [heart-piece](https://www.zeldadungeon.net/wiki/File:TWW-Piece-of-Heart.png) |
| `side-flight-control` | Oltre il traguardo | silver | [deku-leaf](https://zeldawiki.wiki/wiki/File:TWWHD_Deku_Leaf_Artwork.png) |
| `side-lighthouse` | Un faro nella notte | bronze | [lighthouse](https://zeldawiki.wiki/wiki/File:TWW_Kreeb_Model.png) |
| `side-maggie` | Una lettera per Maggy | bronze | [maggie](https://zeldawiki.wiki/wiki/File:TWW_Maggie_Artwork.png), [letter](https://zeldawiki.wiki/wiki/File:TWWHD_Moblin%27s_Letter_Artwork.png) |
| `side-joy-pendants-40` | La carta della maestra | silver | [teacher](https://zeldawiki.wiki/wiki/File:TWW_Mrs._Marie_Artwork.png), [pendant](https://zeldawiki.wiki/wiki/File:TWWHD_Joy_Pendant_Artwork.png) |
| `side-forest-water` | Otto alberelli in fiore | silver | [koroks](https://zeldawiki.wiki/wiki/File:TWW_Korok_Group_Artwork.png), [water](https://zeldawiki.wiki/wiki/File:TWWHD_Forest_Water_Artwork.png) |
| `side-cabana` | Una villa tutta mia | silver | [deed](https://zeldawiki.wiki/wiki/File:TWWHD_Cabana_Deed_Artwork.png) |
| `side-hurricane-spin` | La tecnica dell'uragano | silver | [crest](https://zeldawiki.wiki/wiki/File:TWWHD_Knight%27s_Crest_Artwork.png), [spin](https://zeldawiki.wiki/wiki/File:TWW_Spin_Attack_Artwork.png) |
| `side-orca-500` | Cinquecento colpi | silver | [orca](https://zeldawiki.wiki/wiki/File:TWW_Orca_Artwork.png) |
| `side-town-decoration` | Una città in festa | silver | [town-flower](https://zeldawiki.wiki/wiki/File:TWW_Town_Flower_Render.png), [sea-flower](https://zeldawiki.wiki/wiki/File:TWW_Sea_Flower_Render.png), [exotic-flower](https://zeldawiki.wiki/wiki/File:TWW_Exotic_Flower_Render.png) |
| `side-mila` | Una seconda possibilità | bronze | [mila](https://zeldawiki.wiki/wiki/File:TWW_Mila_Artwork.png), [bottle](https://zeldawiki.wiki/wiki/File:TWWHD_Empty_Bottle_Artwork.png) |
| `side-golden-feathers` | Piume per una promessa | bronze | [feather](https://zeldawiki.wiki/wiki/File:TWWHD_Golden_Feather_Artwork.png) |

## Image register

The manifest also contains the direct original-image download URL and hash.

| Key | Source page | Classification |
| --- | --- | --- |
| baito | [TWW Baito Model.png](https://zeldawiki.wiki/wiki/File:TWW_Baito_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| baton | [TWWHD Wind Waker Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Wind_Waker_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| bees | [TWW Killer Bees Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Killer_Bees_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| big-octo | [TWW Big Octo Model.png](https://zeldawiki.wiki/wiki/File:TWW_Big_Octo_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| bottle | [TWWHD Empty Bottle Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Empty_Bottle_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| charm | [TWWHD Hero's Charm Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Hero%27s_Charm_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| chart | [TWWHD Treasure Chart Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Treasure_Chart_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| crest | [TWWHD Knight's Crest Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Knight%27s_Crest_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| cyclos | [TWW Cyclos Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Cyclos_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| deed | [TWWHD Cabana Deed Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Cabana_Deed_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| deku-leaf | [TWWHD Deku Leaf Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Deku_Leaf_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| exotic-flower | [TWW Exotic Flower Render.png](https://zeldawiki.wiki/wiki/File:TWW_Exotic_Flower_Render.png) | Nintendo promotional artwork/render |
| fado | [TWW Fado Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Fado_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| feather | [TWWHD Golden Feather Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Golden_Feather_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| fig-ganondorf | [TWW Ganondorf Figurine Model.png](https://zeldawiki.wiki/wiki/File:TWW_Ganondorf_Figurine_Model.png) | Published PNG render of original Wind Waker model; not promotional artwork |
| fig-makar | [TWW Makar Figurine Model.png](https://zeldawiki.wiki/wiki/File:TWW_Makar_Figurine_Model.png) | Published PNG render of original Wind Waker model; not promotional artwork |
| fig-medli | [TWW Medli Figurine Model.png](https://zeldawiki.wiki/wiki/File:TWW_Medli_Figurine_Model.png) | Published PNG render of original Wind Waker model; not promotional artwork |
| fishman | [TWWHD Fishman Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Fishman_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| fragment-1 | [Triforce Shard 1 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_1_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-2 | [Triforce Shard 2 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_2_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-3 | [Triforce Shard 3 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_3_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-4 | [Triforce Shard 4 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_4_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-5 | [Triforce Shard 5 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_5_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-6 | [Triforce Shard 6 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_6_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-7 | [Triforce Shard 7 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_7_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| fragment-8 | [Triforce Shard 8 (TWW).png](https://www.zeldadungeon.net/wiki/File:Triforce_Shard_8_%28TWW%29.png) | Published PNG of the original Wind Waker Triforce shard; no model archive used |
| gallery-figurine | [TWW Fado Figurine Model.png](https://zeldawiki.wiki/wiki/File:TWW_Fado_Figurine_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| ganondorf | [TWWHD Ganondorf Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Ganondorf_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| ghost-chart | [TWWHD Ghost Ship Chart Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Ghost_Ship_Chart_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| ghost-ship | [TWW Ghost Ship Model.png](https://zeldawiki.wiki/wiki/File:TWW_Ghost_Ship_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| gohdan | [TWW Gohdan Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Gohdan_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| guru-statue | [TWW Shop Guru Statue Render.png](https://zeldawiki.wiki/wiki/File:TWW_Shop_Guru_Statue_Render.png) | Nintendo promotional artwork/render |
| heart-piece | [TWW-Piece-of-Heart.png](https://www.zeldadungeon.net/wiki/File:TWW-Piece-of-Heart.png) | Published item artwork; check file-page attribution |
| helmaroc | [TWW Helmaroc King Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Helmaroc_King_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| jalhalla | [TWW Jalhalla Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Jalhalla_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| koroks | [TWW Korok Group Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Korok_Group_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| laruto | [TWW Laruto Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Laruto_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| letter | [TWWHD Moblin's Letter Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Moblin%27s_Letter_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| lighthouse | [TWW Kreeb Model.png](https://zeldawiki.wiki/wiki/File:TWW_Kreeb_Model.png) | Published PNG render of original Wind Waker model; not promotional artwork |
| link-baton | [TWW Link Wind Waker Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Link_Wind_Waker_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| maggie | [TWW Maggie Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Maggie_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| makar | [TWWHD Makar Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Makar_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| medli | [TWWHD Medli Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Medli_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| merchant | [TWW Wandering Merchant Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Wandering_Merchant_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| mila | [TWW Mila Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Mila_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| molgera | [TWW Molgera Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Molgera_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| note | [TWWHD Note to Mom Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Note_to_Mom_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| orca | [TWW Orca Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Orca_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| pearls-din | [TWWHD Din's Pearl Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Din%27s_Pearl_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| pearls-farore | [TWWHD Farore's Pearl Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Farore%27s_Pearl_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| pearls-nayru | [TWWHD Nayru's Pearl Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Nayru%27s_Pearl_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| pendant | [TWWHD Joy Pendant Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Joy_Pendant_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| puppet | [TWW Puppet Ganon Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Puppet_Ganon_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| salvatore | [TWW Salvatore Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Salvatore_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| sea-flower | [TWW Sea Flower Render.png](https://zeldawiki.wiki/wiki/File:TWW_Sea_Flower_Render.png) | Nintendo promotional artwork/render |
| shards | [Triforce Shards](https://zelda.nintendo.com/about/) | Nintendo official site, identified as Wind Waker |
| special-chart | [TWWHD Special Chart Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Special_Chart_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| spin | [TWW Spin Attack Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Spin_Attack_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| statue-dragon | [TWW Dragon Tingle Statue Model.png](https://zeldawiki.wiki/wiki/File:TWW_Dragon_Tingle_Statue_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| statue-earth | [TWW Earth Tingle Statue Model.png](https://zeldawiki.wiki/wiki/File:TWW_Earth_Tingle_Statue_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| statue-forbidden | [TWW Forbidden Tingle Statue Model.png](https://zeldawiki.wiki/wiki/File:TWW_Forbidden_Tingle_Statue_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| statue-goddess | [TWW Goddess Tingle Statue Model.png](https://zeldawiki.wiki/wiki/File:TWW_Goddess_Tingle_Statue_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| statue-wind | [TWW Wind Tingle Statue Model.png](https://zeldawiki.wiki/wiki/File:TWW_Wind_Tingle_Statue_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| sunken-chest | [TWW Sunken Chest Model.png](https://zeldawiki.wiki/wiki/File:TWW_Sunken_Chest_Model.png) | Published PNG render of the original game model; not a Nintendo promotional illustration |
| sword | [TWWHD Master Sword Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Master_Sword_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| teacher | [TWW Mrs. Marie Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Mrs._Marie_Artwork.png) | Nintendo promotional artwork/render |
| tott | [TWW Tott Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Tott_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| town-flower | [TWW Town Flower Render.png](https://zeldawiki.wiki/wiki/File:TWW_Town_Flower_Render.png) | Nintendo promotional artwork/render |
| triforce-chart | [TWWHD Triforce Chart Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Triforce_Chart_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| water | [TWWHD Forest Water Artwork.png](https://zeldawiki.wiki/wiki/File:TWWHD_Forest_Water_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
| zephos | [TWW Zephos Artwork.png](https://zeldawiki.wiki/wiki/File:TWW_Zephos_Artwork.png) | Nintendo promotional artwork, archived by Zelda Wiki |
