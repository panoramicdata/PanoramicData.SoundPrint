# Benchmark audio sources

The repository contains no recordings and no fingerprint data. The local-only real-music benchmark (`dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- real`) reads Creative Commons licensed music that you download yourself into the gitignored `benchmarks/audio` folder. Only the metadata of the tracks used and the resulting aggregate numbers are committed.

## Dataset

FMA: A Dataset For Music Analysis (Defferrard, Benzi, Vandergheynst, Bresson; ISMIR 2017), https://github.com/mdeff/fma. `fma_small` holds 30 second clips of 8,000 tracks that artists published on the Free Music Archive under Creative Commons licences; `fma_metadata` holds the track titles, artists and licences. The audio is used only to compute and match fingerprints locally for benchmarking. It is not redistributed, so share-alike and no-derivatives terms are not engaged, and tracks under non-commercial licences are used for this internal measurement only. Licences differ per track and are listed below.

How to fetch:

```shell
mkdir -p benchmarks/audio/fma-small benchmarks/audio/fma-metadata
curl -L -o benchmarks/audio/fma_small.zip https://os.unil.cloud.switch.ch/fma/fma_small.zip
curl -L -o benchmarks/audio/fma_metadata.zip https://os.unil.cloud.switch.ch/fma/fma_metadata.zip
unzip benchmarks/audio/fma_small.zip -d benchmarks/audio/fma-small
unzip benchmarks/audio/fma_metadata.zip -d benchmarks/audio/fma-metadata
```

ffmpeg must be on PATH or named by the `SOUNDPRINT_FFMPEG` environment variable. Without the audio the `real` command prints a SKIPPED message and exits with code 2.

## Tracks used (600: the first 480 form the database, the rest are held out as absent queries)

| FMA track id | Artist | Title | Licence |
|---|---|---|---|
| 2 | AWOL | Food | Attribution-NonCommercial-ShareAlike 3.0 International |
| 204 | Ed Askew | My Dream | Attribution-NonCommercial-ShareAlike 3.0 International |
| 546 | Charles Manson | If You Have No One | Attribution-Noncommercial-Share Alike 3.0 United States |
| 704 | Fósforo | Mano De Dios | Attribution-NonCommercial-ShareAlike 3.0 International |
| 822 | Hecuba | Tom & Jerry | Attribution-NonCommercial-ShareAlike 3.0 International |
| 1069 | Lucky Dragons | Un Lagrima en la Discoteca | Attribution-Noncommercial-Share Alike 3.0 United States |
| 1270 | Necropolis | Cocksuckerbastardmotherfucker | Attribution-Noncommercial-Share Alike 3.0 United States |
| 1661 | So Cow | Only Fools and Horses Theme | Attribution-Noncommercial-Share Alike 3.0 United States |
| 1689 | The Sounds of Taraab | Unalo | Attribution-Noncommercial-Share Alike 3.0 United States |
| 1893 | Tommy Jay | Memories | Attribution-NonCommercial-ShareAlike 3.0 International |
| 3270 | Arborea | Song for Obol |  |
| 3598 | Ed Askew | Little Eyes | Attribution-Noncommercial-Share Alike 3.0 United States |
| 3766 | Kinski | Rhode Island Freakout | Attribution-Noncommercial-Share Alike 3.0 United States |
| 3905 | Michael Chapman | Twisted Road | Attribution-NonCommercial-ShareAlike 3.0 International |
| 4022 | Negative Approach | Can't Tell No One | Attribution-NonCommercial-ShareAlike 3.0 International |
| 4077 | Pistolera | Algo Pa que te Olvides | Attribution |
| 4100 | Polka Madre | Gitanas Mojadas | Attribution-Noncommercial-Share Alike 3.0 United States |
| 4508 | Yan Yalego | Five Foot Two | Attribution-Noncommercial-Share Alike 3.0 United States |
| 4778 | Half Japanese | Too Much Fun | Attribution-NonCommercial-ShareAlike 3.0 International |
| 4838 | Peelander-Z | Punk Rock | Attribution-Noncommercial-Share Alike 3.0 United States |
| 5264 | Dälek | Classical Homicide | Attribution-NonCommercial-ShareAlike 3.0 International |
| 6342 | Zikrayat | Doulab Nahawand Murassaa | Attribution-Noncommercial-Share Alike 3.0 United States |
| 6379 | The Sounds of Taraab | Matamu Choz | Attribution-Noncommercial-Share Alike 3.0 United States |
| 6407 | MV + EE | Huna Cosm | Attribution-Noncommercial-Share Alike 3.0 United States |
| 6603 | Height With Friends | The Woods | Attribution-Noncommercial-Share Alike 3.0 United States |
| 6684 | Jarrod Fowler | Preface | Creative Commons Attribution |
| 6857 | MV + EE and Willie Lane | Drone Trailer | Attribution-Noncommercial-Share Alike 3.0 United States |
| 7388 | Lucky Dragons | girlworld (3 in a bed mix) | Attribution-NonCommercial-ShareAlike 3.0 International |
| 7526 | Monk Turner | Easy on the Eyes | Attribution-NonCommercial |
| 8208 | Alan Vega & Oneida | Rocket USA | Attribution-Noncommercial-Share Alike 3.0 United States |
| 9477 | Justice Yeldham | Colour Me Your Colour | Attribution-Noncommercial-Share Alike 3.0 United States |
| 9721 | Justice Yeldham | Track 01 | Attribution-Noncommercial-Share Alike 3.0 United States |
| 10381 | Big Blood | Baron in the Trees | Attribution-Noncommercial-Share Alike 3.0 United States |
| 10441 | Big Blood | In The Shade | Attribution-Noncommercial-Share Alike 3.0 United States |
| 10575 | Bobb Trimble | Premonitions: The Fantasy | Attribution-Noncommercial-Share Alike 3.0 United States |
| 10679 | Big Blood | The Rise Of Quinnisa Rose | Attribution-Noncommercial-Share Alike 3.0 United States |
| 10808 | Lesbian Afternoon | Wind Up The Hypnotist | Attribution-Noncommercial-Share Alike 3.0 United States |
| 11206 | Music Komite | Elles | Creative Commons Attribution |
| 11334 | Gen Ken Montgomery | Instrumental | Attribution-Noncommercial-Share Alike 3.0 United States |
| 11679 | Cordero | Ruleta Rusa | Attribution-Noncommercial-Share Alike 3.0 United States |
| 11772 | 6th Sense | Ignite The People (Like Obama) Instrumental     {6th Sense} | Attribution-NonCommercial 3.0 International |
| 11785 | 6th Sense | AM Set Instrumental     {Both Nice} | Attribution-NonCommercial 3.0 International |
| 11839 | 6th Sense &amp; Mick Boogie | Belief feat. John Mayer | Attribution-Noncommercial-Share Alike 3.0 United States |
| 11937 | DavidBowman | Wir können auch anders | Attribution-Noncommercial-No Derivative Works 3.0 Germany |
| 12053 | Big Blood | Land Upon Mud II | Attribution-Noncommercial-Share Alike 3.0 United States |
| 12173 | Coehn und Foehrb | Mit der Schaufel aufs Ei | Attribution-Share Alike 3.0 Germany |
| 12376 | The Harvey Girls | Good Morning, Bubblegum | Attribution-NonCommercial-ShareAlike 3.0 International |
| 12508 | Big Blood | Satie (First Gnossienne) | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 12654 | Bird Names | People Should Get More Aware | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 13325 | Josh Woodward | Methylchloroisothiazolinone | Attribution-NonCommercial-ShareAlike 3.0 International |
| 13571 | L'Onomatopeur | Pour Petite Tete | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 13735 | Tha Connection | Funktions Prod. Sci Fi | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 14315 | Black Sea Hotel | Dimjaninka | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 14539 | Opio | Intro | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 14581 | Ergo Phizmiz | Dance With a Ball of Emperor Rudolf II | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 14661 | Jonathan Coulton | Re: Your Brains | Attribution-NonCommercial 3.0 International |
| 14741 | Throwing Muses | Limbo | Attribution-Noncommercial-Share Alike 3.0 United States |
| 15476 | Bruce Russell | Wehowsky Loop Blues | Attribution 3.0 United States |
| 15880 | Kokusai Bunka Shinkokai | Taishikichô Chôshi | Attribution-Noncommercial-Share Alike 3.0 United States |
| 16744 | Salam | Drogue | Attribution-NonCommercial-ShareAlike 3.0 International |
| 16997 | Tusk Lord | Third Christmas Song | Attribution-Noncommercial-Share Alike 3.0 United States |
| 17607 | Alash Ensemble | Kargyraa | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 17735 | Liturgy | Ecstatic Rite | Attribution-Noncommercial-Share Alike 3.0 United States |
| 18112 | Sikhara | Bardos | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 19073 | Bell X1 | One Stringed Harp | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 19422 | Orquestra Popular De Paio Pires | Nothing Happens | Attribution-NonCommercial-ShareAlike 3.0 International |
| 19708 | Steffen Basho-Junghans | Sky Dreamer's Gold | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 20365 | Ergo Phizmiz | Transformer | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 20818 | Harrius | 03 The Wedding Ring | Attribution-Noncommercial-Share Alike 3.0 United States |
| 21409 | Peter Fuhry | Japanese Robot Likes Sushi, Fears President | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 21891 | Mikuś | Sunrise, Nightfall | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 22095 | Just Plain Ant | Restless | Attribution-No Derivative Works 3.0 United States |
| 22478 | Ergo Phizmiz | Something Dirty | Attribution 3.0 United States |
| 23155 | Lee Maddeford | Bouchon (with Les Gauchers Quintet) | Attribution-NonCommercial-ShareAlike 3.0 International |
| 24362 | Cuban Cowboys | Outro | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 24422 | Black Ant | Boomerang | Creative Commons Attribution |
| 24521 | Irene Moon & Sara O'Keefe | Flightless For Candy | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 24746 | Gipsyork | Tutti Frutti / Portocala | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 24983 | Ergo Phizmiz | Singing in the Raindrops Keep | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 25216 | Zlatne Uste Balkan Brass Band | Kolo Iz Sumadije | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 25609 | Ras Amerlock | Leipzig Im Winter | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 26012 | Comfort Fit | True Form | Attribution-Noncommercial-No Derivative Works 3.0 Germany |
| 26169 | Monk Turner | MJ's Grave | Attribution-NonCommercial |
| 26629 | NOISE64 | Heroic Hexa Epic | Attribution-NonCommercial-ShareAlike 3.0 International |
| 26657 | Graphiqs Groove | Reed Gray | Attribution-Noncommercial-Share Alike 3.0 United States |
| 27164 | Rhoda | thisis a humalong (feat. featherbeard) | Attribution-Noncommercial-Share Alike 3.0 United States |
| 27549 | Frau | Rat and Cat | Attribution-NonCommercial-ShareAlike 3.0 International |
| 27799 | Ms.Vybe | Elevator | Attribution-NonCommercial 3.0 International |
| 27987 | Gamelan Nyai Saraswati | Bubaran UDAN MAS | Attribution-Noncommercial 3.0 United States |
| 28480 | The Pop Winds | Will Be A Dream | Attribution-Noncommercial-NoDerivatives 2.5 Canada |
| 29037 | Digi G'Alessio | raw instrumental pt.1 | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 29271 | Dan Bryk | Discount Store | Attribution-NonCommercial-ShareAlike 3.0 International |
| 29718 | Codex Machine | Controlled Relaxation | Attribution-Noncommercial-Share Alike 3.0 United States |
| 29750 | Pastor McPurvis | The Little Monkey Song | Attribution-NonCommercial-ShareAlike 3.0 International |
| 30090 | 1 Riche Kannibal | Tribal 23 Tribute | Attribution-Noncommercial-No Derivative Works 3.0 Germany |
| 30522 | Trash80 | At Teh Disko | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 31356 | Spectre Folk | Toot Toot | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 32075 | Single Bullet Theory | Rocker's Night Out (Punk for a Day) | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 32335 | Plushgoolash | Legs on a Wine Glass | Attribution-NonCommercial-ShareAlike 3.0 International |
| 32687 | transient | You and Me | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 33050 | Eric Lindley | It's Funny | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 33278 | Bing and Ruth | Rails | Attribution-NonCommercial-ShareAlike 3.0 International |
| 33992 | The Pleasure Kills | Oblivion | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 35008 | Gurdonark | Sadness | Creative Commons Attribution |
| 35535 | Lorenzo's Music | Old Old Story | Attribution-ShareAlike 3.0 International |
| 36096 | Exteenager | your name is rain | Attribution-NonCommercial 3.0 International |
| 36273 | Demiran Ćerimović and His Orkestar | Competition Rehearsal, Song 2 | Attribution-Noncommercial-Share Alike 3.0 United States |
| 36526 | Lou Barlow | Faith Defies the Night | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 36966 | Garmisch | Glimmer (mGee's the Boom and the Bap Remix) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 37041 | MV Carbon & Philip White | Improvisation | Attribution-Noncommercial-Share Alike 3.0 United States |
| 37416 | Church Of When The Shit Hits The Fan | Octo Gate | Attribution-Noncommercial-Share Alike 3.0 United States |
| 37911 | Limonadiac | A Dyslexic Dumb Dead Voice | Public Domain |
| 38365 | Mastermind XS | Stop that mission (instrumental) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 38781 | Sláinte | Denis Murphy's, John Ryan's | Attribution-Share Alike 3.0 United States |
| 38825 | Sláinte | Lark in the Morning. The Atholl Highlanders | Attribution-Share Alike 3.0 United States |
| 38881 | File Under Toner | and who gives a shit | Public Domain |
| 38898 | File Under Toner | violins | Public Domain |
| 38914 | File Under Toner | whip me, christy | Public Domain |
| 39317 | John Badger & The Moustache Riders of Doom | Brave New World | Attribution-Noncommercial-Share Alike 3.0 United States |
| 39661 | Quiet Hooves | AVP | Attribution-Noncommercial-Share Alike 3.0 United States |
| 40133 | Behold! The Living Corpse | Two Headed Baby | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 40234 | Big Blood | Oh My Child | Attribution-Noncommercial-Share Alike 3.0 United States |
| 40509 | Richard Garet | Light Field (excerpt) | Attribution-Noncommercial-Share Alike 3.0 United States |
| 40725 | Str8 Sounds | It's My Life Welcome To It | Attribution-Noncommercial-Share Alike 3.0 United States |
| 40986 | The Corin Tucker Band | Doubt | Attribution-Noncommercial-Share Alike 3.0 United States |
| 41573 | Janne Hanhisuanto | Autumn | Attribution-Noncommercial-No Derivative Works 2.5 Italy |
| 42014 | Caballo and the Mothafu Kings | Skango - Volfoniq rmx (Canada) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 42045 | The Insider | The Great Break-off | Attribution-ShareAlike 3.0 International |
| 42236 | Zlatne Uste Balkan Brass Band | Kozarica Kolo | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 42377 | Broke For Free | Night Owl | Creative Commons Attribution |
| 42915 | Phemale | FACE FARM | Attribution-Noncommercial-Share Alike 3.0 United States |
| 43027 | Huun Huur Tu | [interview] | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 43533 | MWE | Halay | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 43595 | Gipsyork | Intr-o Zi La Poarta Mea | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 43699 | MWE | Varshaver | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 43858 | This One | tell me how | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 43911 | This One | fresh handmade | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 44779 | Giovanni Cipolla | Monsters | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 44800 | Aleksi Eeben | Avaruusmiehen Pieni Askel (Spaceman's Small Step) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 44849 | Sunslide | Field Piano 2 | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 44952 | SONGO 21 | Opening para Songo 21 | Attribution-NonCommercial-ShareAlike 3.0 International |
| 45149 | Nisiru | Magyar Skweeast | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 45393 | Blear Moon | Le dernier | Attribution-NonCommercial-ShareAlike 3.0 International |
| 45522 | Plusplus | Windings | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 46160 | Poorpot | kaze no tani no naushika | Attribution-ShareAlike 3.0 International |
| 46733 | KASHKA | Hands In | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 47068 | Eric Skiff | A Night Of Dizzy Spells | Creative Commons Attribution |
| 47194 | Asian Women on the Telephone | Chrome Weels | Attribution-NonCommercial 3.0 International |
| 47526 | Bare Wires | Romantic Girl | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 47707 | Blue Sausage Infant | Comtrex | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 48043 | Delphine Dora | The Road To Unknown | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 48442 | Bass Assassin | 8bitDetective | Attribution-NonCommercial-ShareAlike 3.0 International |
| 48465 | Sycamore Drive | Ocean Breeze | Attribution-Noncommercial-Share Alike 3.0 United States |
| 48863 | NaDa BaBa | Thunder Wave | Attribution-NonCommercial-ShareAlike 3.0 International |
| 49067 | Justice Yeldham | Buscuit Basket | Attribution-Noncommercial-Share Alike 3.0 United States |
| 49477 | Garmisch | U Fine (Remix) (Featuring CM aka Creative) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 49856 | My Bubba & Mi | Blinde state | Attribution-NonCommercial-ShareAlike 3.0 International |
| 50539 | The Men | Stranger Song | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 51112 | Bob Ostertag | Arms and Legs | Creative Commons Attribution |
| 51263 | Germany Germany | Through The Noise | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 51292 | Phemale | seven cities of mold | Attribution-Noncommercial-Share Alike 3.0 United States |
| 51923 | Flamingo | Superpalm | Attribution-NonCommercial-ShareAlike 3.0 International |
| 52040 | Poland | Catfishes | Attribution-NonCommercial |
| 52126 | Cracked Latin | Interview Segment 3 | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 52449 | Jan Morgenstern | Expedition | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 52632 | Jason Shaw | Boom | Attribution 3.0 United States |
| 52647 | Jason Shaw | Sk8board | Attribution 3.0 United States |
| 52951 | Marissa Nadler | Alabaster Queen | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 53299 | Mikuś | Dub For Alicia | Attribution-NonCommercial-ShareAlike 3.0 International |
| 53589 | Big Blood | Keening | Attribution-Noncommercial-Share Alike 3.0 United States |
| 53863 | The Easton Ellises | Endorphin | Attribution-NonCommercial-ShareAlike 3.0 International |
| 54149 | Mark Neil | 01 Shakey3 | Attribution-Noncommercial-Share Alike 3.0 United States |
| 54235 | Ivy | Fascinated | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 54463 | DuBoLoGy | Swan Lake Dub | Creative Commons Attribution |
| 54482 | Friglob | Love music | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 54662 | Noisewaves | Horses With Fake Legs | Attribution-Noncommercial-Share Alike 3.0 United States |
| 55100 | Alaclair Ensemble | Womp | Attribution-ShareAlike 3.0 International |
| 55231 | Mastermind XS | All Back To Mine | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 55287 | The Kyoto Connection | The best days of my life | Attribution-Noncommercial-NoDerivatives 2.5 Argentina |
| 55480 | 320320320320 | The Zombies Attack | Attribution-ShareAlike 3.0 International |
| 55718 | et_ | Kopeika | Attribution-NonCommercial-ShareAlike 3.0 International |
| 55830 | Sexteto Tabalá de Palenque | Agua | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 56034 | Sans Blanc | dykedub (dyke b mix) | Attribution-Noncommercial-Share Alike 3.0 United States |
| 56469 | Alash Ensemble | Yrlaazhyly | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 56518 | Jenny Mayhem | When You're Older | Attribution-NonCommercial-ShareAlike 3.0 International |
| 56649 | Jackie & The Cedrics | Osaka Holiday | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 56696 | Corty | New World Order - Vol 2 (vocals: Revolushan) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 56888 | John King | soloVIOLA | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 57273 | Sol Rezza | The existence of the light | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 57540 | Ralp | Oxuria Memoth | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 57663 | Shark? | Hey Grrl | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 57938 | R.Tists for Christmas | 12 Days of Christmas | Attribution-NonCommercial-ShareAlike 3.0 International |
| 58116 | The Golden Hours | My Feet Beneath The Water | Attribution-Noncommercial-Share Alike 3.0 United States |
| 58212 | Elemint | Guerrilla Music | Attribution-ShareAlike 3.0 International |
| 58543 | Man Bites Dog | College Rock (How To Hold The Guitar Right) | Attribution-ShareAlike 3.0 International |
| 59581 | Circus Marcus | Búsqueda exploratoria #005 - Tu pars.mp3 | Attribution-NonCommercial 3.0 International |
| 59671 | Slaveya Women's Vocal Ensemble | Dark Eyed Girl | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 59686 | Ventanas | Introductions and Chifteteli | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 59720 | The Rosen Sisters | Zemer Atik | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 60042 | Lessazo | Intro Tie ba té | Art Libre |
| 60476 | Mokhov | Perfect Tomorrow | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 60736 | Mexican Holiday | Mexican Holiday live at the Winston | Attribution-Noncommercial-Share Alike 3.0 United States |
| 60871 | Eastern Watershed Klezmer Quartet | Avante Me Fante | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 61159 | Alaclair Ensemble | Drive Thru | Attribution-ShareAlike 3.0 International |
| 61591 | Ergo Phizmiz | Gigglestick | Attribution-NonCommercial-ShareAlike 3.0 International |
| 62003 | Armageddon Gospel Revival | Free You | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 62189 | Armée | Génie militaire | Attribution-Noncommercial-Share Alike 3.0 United States |
| 62448 | Jahzzar | Julia | Attribution-ShareAlike 3.0 International |
| 62534 | Tommy Tornado | Closing | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 62750 | Jahzzar | Roller-skate | Attribution-ShareAlike 3.0 International |
| 63208 | Larkin Grimm | The Road Is Paved With Leaves | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 63291 | I Was A Teenage Strangler Soundtrack | March of the Teenagers pt. 2 | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 63770 | Cullah | Western Firefight 2 | Attribution-ShareAlike |
| 64007 | Gosprom | Nevesta | Attribution-NonCommercial-ShareAlike 3.0 International |
| 64331 | Pierlo | Promo Mix Vocal Edit (Luv Dub Mix) | Creative Commons Attribution |
| 64516 | Heavy Links | The Way | Attribution-ShareAlike 3.0 International |
| 64556 | Wellington Boot | Birthquake | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 64628 | Blank & Kytt | Come Get Some | Creative Commons Attribution |
| 64854 | Monroeville Music Center | Swedish Fish | Creative Commons Attribution |
| 64893 | Buildings and Mountains | horse and sparrow | Attribution-NonCommercial-ShareAlike 3.0 International |
| 65037 | Candlegravity | A Lifetime of Summers Past | Attribution-NonCommercial-ShareAlike 3.0 International |
| 65619 | BlackBox Red | BlackBox Red at Vondelbunker | Attribution-Noncommercial-Share Alike 3.0 United States |
| 66074 | LASERS | Porto | Attribution-NonCommercial-ShareAlike 3.0 International |
| 66535 | Waylon Thornton | Baladora | Attribution-Noncommercial-Share Alike 3.0 United States |
| 66649 | The Suzan | Uh-Ah | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 67121 | A Banda de Joseph Tourton | #3 | Music Sharing |
| 67357 | Pompey | 50 Gallon Drum | Attribution-NonCommercial |
| 67457 | Big City Orchestra | a kiss | Attribution-NonCommercial 3.0 International |
| 67640 | DZZ | Nightmare | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 68353 | Caroline Gorman | Hiding in the Dark | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 68540 | Benboncan | Murmur | Creative Commons Attribution |
| 68682 | Mean Moon | Artificial Being | Attribution-Noncommercial-Share Alike 3.0 United States |
| 68852 | Howie & Ann Mitchell | Temperance Reel | Attribution-Noncommercial-Share Alike 3.0 United States |
| 68895 | Broke For Free | Luminous | Attribution-NonCommercial 3.0 International |
| 69194 | Risey | Memories Of Thailand (Beat Doctor's 'stuck in Britain' remix) | Attribution-NonCommercial 3.0 International |
| 69207 | Space Invaders v Psychadelik Pedestrian | By The Water (Aerologic remix) | Attribution-NonCommercial 3.0 International |
| 69564 | The NSJ Crew | 'Til You Get There (ft. Shawntel of the Y.N.RichKids) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 69763 | Plusplus | Kerouak | Attribution-Noncommercial-Share Alike 2.5 Italy |
| 69792 | Chris Cook | Place Like No Home | Attribution-NonCommercial-ShareAlike 3.0 International |
| 69947 | White Wires | Ha Ha Holiday | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 70303 | Morsa | Erotic Post-Ironic (feat. Ella) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 70770 | S.J. Mellia | Murder Death Kill | Attribution-Noncommercial-Share Alike 3.0 United States |
| 71158 | C-Doc | (Story) Telling (Featuring Marcus J Gilligan) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 71243 | Dengaz | Encontrei (feat. Agir) | Attribution-ShareAlike 3.0 International |
| 71276 | Pink Reason | The Devil Always Wins | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 71512 | Quelle Fromage | Noam Chomsky's Corporate Carbohydrate Surprise | Creative Commons Attribution |
| 71695 | C-Doc | Milk O' Magnesia (Instrumental) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 72046 | David Rovics | Minimum Wage Strike | Attribution-Noncommercial-Share Alike 3.0 United States |
| 72073 | Decktonic | Z-Saber (feat Jake Allison) | Attribution |
| 72206 | Music For Your Plants | Stabmixer Bazar | Attribution-NonCommercial 3.0 International |
| 72477 | Fhernando | Last Days of Disco | Attribution-NonCommercial-ShareAlike 3.0 International |
| 72783 | Tha Silent Partner | Something 4Her2 Strip2 | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 73099 | Jaap Blonk & Dylan Nyoukis | Small Teeth | Attribution-Noncommercial-Share Alike 3.0 United States |
| 73306 | Ex Cops | Springbreak (Happy Birthday) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 73368 | Deltason | Flow Of The River | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 73519 | Hounds Of Hate | Evil That Men Do | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 73573 | James Beaudreau | Devotional | Attribution-Noncommercial-Share Alike 3.0 United States |
| 73766 | Shoes and Socks Off | Tork Sport | Attribution-NonCommercial 3.0 International |
| 73779 | Mezonom | Deep Cut | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 74365 | Mizuki's Last Chance | Yeah | Attribution-NoDerivs 2.0 UK: England & Wales |
| 74383 | The Gasoline Brothers | Hungover Boxing Day | Attribution-NonCommercial 3.0 International |
| 74546 | Entertainment for the Braindead | Solitude | Attribution-NonCommercial-ShareAlike 3.0 International |
| 75211 | SOB Pariassound | Noodles and commercial TV | Attribution-ShareAlike 3.0 International |
| 75377 | Kellee Maize | Shake Dat | Creative Commons Attribution |
| 75398 | Kellee Maize | Pulse | Creative Commons Attribution |
| 75429 | Cherly KaCherly | Underwater sleep while trying not to breath | Attribution-NonCommercial-ShareAlike 3.0 International |
| 75442 | Cherly KaCherly | Outside the water drop there's more rain drops. We jump from one to the other on our way to the pond | Attribution-NonCommercial-ShareAlike 3.0 International |
| 75754 | Kellee Maize | Struggle | Creative Commons Attribution |
| 75862 | Jonah Rapino | 19. Male Fantasy | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 75932 | Tricky Diesel | World of Concrete | Creative Commons Attribution |
| 76076 | Koliadnyky of Kryvorivnia | Tsymbaly Solo | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 76440 | Misay Day | Imaginary Blues | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 78156 | Burning Bright | Stardust | Attribution-Noncommercial-Share Alike 3.0 United States |
| 78851 | Stray | Chuço | Attribution-NonCommercial-ShareAlike |
| 79975 | Emanuele Costantini | ITA Meda - Milano - Ambience Ext Italy Birds Children IRCA | Attribution-NoDerivatives 3.0 International |
| 80005 | Brock Tyler | My Love For You | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 80611 | S.J. Mellia | Zucotic        Co Prod with form No Drums Version 2005 | Attribution-Noncommercial-Share Alike 3.0 United States |
| 80756 | Gogofski | Chaje Shukarije | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 80836 | Origami Repetika | mighty lilbumble bee | Attribution-NonCommercial 3.0 International |
| 81365 | Maya Solovéy | Little Drummer Boy | Attribution-Noncommercial-Share Alike 3.0 United States |
| 81600 | Podington Bear | Euphoric | Attribution-NonCommercial 3.0 International |
| 81868 | Expert In Mental | Pavillon Dubstep | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 82462 | Throwing Muses | Stand Up | Attribution-Noncommercial-Share Alike 3.0 United States |
| 82892 | Mice Parade | Warm Hand in Farmland | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 82931 | krackatoa | Hefty Leftie Drop Attack | Attribution-NonCommercial-ShareAlike 3.0 International |
| 83613 | BOPD | New England Dawn Salendro | Attribution-NonCommercial 3.0 International |
| 83913 | Rodd Keith | Why Don't You Sing A Happy Song | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 84097 | Le Perche Oreille | Fin | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 84158 | Laura Stevenson and the Cans | Source and the Sound | Attribution-Noncommercial-Share Alike 3.0 United States |
| 84483 | BLEO | m00m4m4 feat. Ovenrake | Attribution-Noncommercial-Share Alike 3.0 United States |
| 85038 | Sick To The Back Teeth | Quinine Dub | Attribution-NonCommercial-ShareAlike 3.0 International |
| 85343 | L'homme Manete | Epic Champ | Attribution-NonCommercial-ShareAlike 3.0 International |
| 85428 | Orange Peels | Yonder | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 85494 | Anonymous | STAR SPANGLED BANNER | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 85788 | Rations | Occasion For War | Attribution-ShareAlike 3.0 International |
| 85836 | Frame | Aproaching Land | Attribution-NonCommercial-ShareAlike 3.0 International |
| 85963 | Star Rover | Portland Cement Factory at Monolith, CA (John Fahey) & The Harmony of Altamont (John Fahey) | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 86117 | Garmisch | And Now We Stand Against The Wall (Who Ha's Electron Takeover) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 86443 | Fever Dream | Befall | Attribution-Noncommercial-Share Alike 3.0 United States |
| 86679 | Krowne | Welcome To Warp Street | Attribution-Noncommercial-Share Alike 3.0 United States |
| 86799 | Von Korf | Marktversagen | Attribution-Noncommercial-Share Alike 3.0 Germany |
| 87104 | XPURM | CNUNU | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 87159 | Dr. Wylie | Keep In Style | Attribution-Noncommercial-Share Alike 3.0 United States |
| 87237 | Kris Keyser | Only | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 87641 | Lantriperc | Level Up | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 88485 | Naval Cassidy | Pea Surgery | Attribution-NonCommercial-ShareAlike 3.0 International |
| 88869 | Josh Woodward | Gallows Hill (No Vocals) | Attribution 3.0 United States |
| 88900 | Hunter-Gatherer | Magpie Trap | Attribution-Noncommercial-NoDerivatives 2.5 Canada |
| 89447 | Anitek | One Way Ticket (Featuring Tab & D Spliff) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 89816 | ENDRUDARK | Time Up | Creative Commons Attribution |
| 90074 | Egg Nebula | Mental Communication | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 90591 | Pilesar | In the Raw (bonus) | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 90982 | Mported Flows | Gangstas, Guns, Liquor | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 91093 | Cuntz | Birthday Song | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 91177 | Aislinn | Drops of brandy - The mountain kid | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 91306 | BADLUCK | NY Don't Sleep (Luckstrumental) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 91468 | Mystery Pills | By Design | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 91793 | Three Jolly Rogues | Little Birdie | Attribution-Noncommercial-Share Alike 3.0 United States |
| 91937 | Zachary Cale | Love In Vain | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 92284 | DJ Spooky | Of Water and Ice feat. Jin-Xiang JX Yu (Dubstep Remix - Bonus Track) | Attribution-NonCommercial-ShareAlike 3.0 International |
| 92551 | Marc Burt | Twenty Ten | Attribution-NonCommercial 3.0 International |
| 92878 | The Fucked Up Beat | A Sad Song for Junction City Fields | Attribution-ShareAlike 3.0 International |
| 92953 | Muhmood | Organ Etude | Attribution-NonCommercial-ShareAlike 3.0 International |
| 93523 | Moon Veil | Up There | Creative Commons Attribution |
| 93921 | Jahzzar | Wet Socks | Attribution-ShareAlike 3.0 International |
| 94033 | Coldnoise | Life Of Butterflies | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 94411 | Kakurenbo | Ichill | Attribution-NonCommercial 3.0 International |
| 94631 | Mike Morton | Environnement : Jungle sound | Attribution-Noncommercial-Share Alike 3.0 United States |
| 95309 | Mário Gajarský | Great Music Day | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 95910 | Suicidal Rap Orgy | Murder Rap | Attribution-Noncommercial-Share Alike 3.0 United States |
| 96403 | Insane Ride | Wrong or Right | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 96698 | Melissa Laveaux | Pretty Girls | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 96900 | Sudden Infant | Catharsis | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 97216 | The Wingdale Community Singers | When I Heard The Learn'd Astronomer | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 97542 | Amitron_7 | Lotus Flower | Attribution-NonCommercial-ShareAlike 3.0 International |
| 97590 | Olivia Chaney | The King's Horses | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 97847 | Egg Nebula | Unknown Resource | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 97987 | Deadly Combo | Incredible Things (Featuring Golden Child) (Accapella) | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 98227 | RoccoW | Weeklybeats 2014 #2 - Daniel's Kruis | Attribution-ShareAlike |
| 98302 | Rakiya | Sweet Girl | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 98555 | Kellee Maize | Gotta Love Me | Attribution |
| 98580 | Hi-Rez | I Know (Prod. MVSIC) | Attribution |
| 98623 | KINGS OF THE CITY | Dust at the Heel of Horror | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 98670 | Scott Wilson & Efendi | Drum Solo / Bayolunda | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 99214 | Julian Winter | Broad Boardwalk | Attribution-Noncommercial-Share Alike 3.0 United States |
| 99370 | Yasna New York Bulgarian Women's Voices Choir | Dilmano Dilbero | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 99411 | Pampers | Guts | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 100478 | LOWdown | So On and Yo! | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 100802 | Margina | U vatru | Attribution-NonCommercial-ShareAlike 3.0 International |
| 101119 | La Sabrosa Sabrosura | Papaya | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 103521 | Novi Split | Young Girls | Attribution-NonCommercial 3.0 International |
| 104066 | Boogarins | Hoje Aprendi de Verdade | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 104357 | Mark Fosson | Softer Place To Fall | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 105145 | Mikroben Krieg | Sleepwalkers (remixed by Waste Disposal Machine) | Attribution-NonCommercial-ShareAlike 3.0 International |
| 105671 | Gilo | Lei#4 | Attribution-NonCommercial-ShareAlike 3.0 International |
| 105718 | Sam Moss | Wildwood Flower | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 105916 | Matt Sowell | Raga | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 106463 | Dinosaur Jugnot | Sorry I am a bad boy | Attribution-Noncommercial-Share Alike 3.0 United States |
| 106629 | Phelios | IV | Attribution-NonCommercial-ShareAlike 3.0 International |
| 107021 | Alaclair Ensemble | FASTLANE (avec Kaytranada) | Attribution-ShareAlike |
| 107125 | Marrach | Red Waters | Attribution-NonCommercial-ShareAlike 3.0 International |
| 107186 | Don Goliath | Let us prais Jahoviah Dub | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 107391 | Aussitôt Mort / MORT MORT MORT | Mort! Mort! Mort! | Attribution-NonCommercial-NoDerivs 3.0 France |
| 107533 | Vägskäl | The Night The Earth Stood Still | Attribution-NonCommercial-ShareAlike |
| 107589 | Kelly Latimore | Ode to Humming | Attribution |
| 107659 | Tony Gage | 80s Action Movie | Attribution-Noncommercial-Share Alike 2.0 UK: England  |
| 107912 | Project 5am | The Dream | Attribution-NonCommercial-ShareAlike |
| 108015 | Zach Fleury | a Murden Lullaby. | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 108031 | Wann | Happy Birthday | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 108298 | Celebration | Kilimanjaro | Attribution-Noncommercial-Share Alike 3.0 United States |
| 108318 | Cagey House | Angels for Feldman | Attribution-NonCommercial-ShareAlike |
| 108425 | cultureculture | Use Me | Attribution-Noncommercial-Share Alike 3.0 United States |
| 108473 | Alex Mason | Sunrise and Sunset | Attribution-NonCommercial-ShareAlike |
| 108494 | Sun Sunych ft. Marina Zakharchenko | Life (ft. Marina Zakharchenko) | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 108525 | Ghost By Nature | Quest | Attribution-Noncommercial-Share Alike 3.0 United States |
| 108809 | D3Zs | The Zombies (Featuring T-Pick) | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 108863 | Black Sea Hotel | Trendafilcheto, Karamfilcheto | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 108885 | Plusplus | The Rolling Hills of England | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 109072 | Rene Kita | Constipation Boogie | Attribution-NonCommercial-ShareAlike |
| 109445 | Stephen Bartolomei | Tonight We Will Rise | Attribution-Noncommercial-No Derivative Works 3.0 United States |
| 109542 | Newton Balloon | Air Born | Attribution-NonCommercial-ShareAlike |
| 109901 | Debby Schwartz | Dreaming New York City in the Middle of LA | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 109972 | Phemale | X | Attribution-Noncommercial-Share Alike 3.0 United States |
| 110103 | Azureflux | Popcorn Blast | Attribution-NonCommercial-ShareAlike |
| 110204 | Circus Marcus | La tapa del domingo | Attribution-NonCommercial 3.0 International |
| 110265 | Kellee Maize & J. Glaze & Udachi | Fact Is Remix (J. Glaze and Udachi Remix) | Attribution |
| 110447 | Papascandy | Feeling | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 110636 | Eidetic Seeing | It's Not Blood It's Gamelan | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 110688 | Anima | The Beating Sun | Attribution-NonCommercial-ShareAlike |
| 110771 | The Impossebulls | For More Years (Instrumental) | Attribution-NonCommercial-ShareAlike |
| 110983 | Antethic | Morning Glory | Attribution-NonCommercial |
| 111187 | Sondra Sun-Odeon | Oaks | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 111230 | Mystified | Vortex Theme | Attribution-NonCommercial-ShareAlike |
| 111382 | Fierbinteanu | Love Will Keep Us Rocking | Attribution-NonCommercial-ShareAlike 3.0 International |
| 111397 | Fierbinteanu | Think I Fall In Love | Attribution-NonCommercial-ShareAlike 3.0 International |
| 111819 | P C III | Ad Astra | Attribution |
| 111994 | Serakhe0n | Aego | Attribution-NonCommercial-ShareAlike |
| 112210 | The OO-Ray | Willow | Attribution-NonCommercial |
| 112485 | The Impossebulls | Erykah & Jean (Instrumental) | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 112587 | Simon Panrucker | Stacks Of Stuff | Attribution-Noncommercial-Share Alike 3.0 United States |
| 112788 | ODOR BABY | BROKEN NEWS | Attribution-ShareAlike |
| 113021 | Malaventura | El Burrito | Public Domain Mark 1.0 |
| 113035 | Tickle | The Drop (Thomas The Search Engine Version) | Attribution-Noncommercial-Share Alike 3.0 United States |
| 113262 | Cartas a Felice | Volcán | Attribution-NonCommercial-ShareAlike 3.0 International |
| 113275 | iNCH. | Find, Fix & Save | Attribution-NonCommercial-ShareAlike 3.0 International |
| 113311 | Mamá Burger | Tarta pop | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 113513 | A Crooked Pulse | Distorted Course | Attribution |
| 113788 | Phlox.s | Deeper | Attribution-NonCommercial |
| 113950 | Brett Saxon | Atavistic | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 114044 | Aviscerall | Stuck In Dreamland | Attribution-NonCommercial |
| 114067 | Decktonic | Dancing is Permitted | Attribution-NonCommercial-ShareAlike |
| 114222 | Squire Tuck | Ukulele Blues | Attribution |
| 114265 | Uncle Milk | Ruggles (ft epsilon not) | Attribution |
| 114293 | Track Jackit | Night In Chicago | Attribution-NonCommercial-ShareAlike |
| 114386 | Quaro/Bruno Del Ben | Lost | Attribution |
| 114400 | Scott Holmes | Origin ll | Attribution |
| 114414 | Scott Holmes | We Used To Paint Stars In The Sky | Attribution |
| 114545 | epsilon not | shmoove | Attribution |
| 114942 | Zingeray! | Who Will Plant the Seeds of Peace | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 115271 | Catherine Foster Brass Band | Čoček of Vranje | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 115470 | Blue Dot Sessions | Farsical - Interlude | Attribution-NonCommercial |
| 115721 | Martin R. | Blue Pill | Attribution-Noncommercial-Share Alike 3.0 United States |
| 115771 | Jelsonic | Saying Goodbye In The Rain (piano) | Attribution |
| 115850 | Dead Peoples Records | 11am | Attribution-Noncommercial-Share Alike 3.0 United States |
| 115944 | Myriam Gendron | Threnody | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 116236 | Citofono | Deep Window | Attribution-Noncommercial-Share Alike 3.0 United States |
| 116388 | Asthmatic Astronaut | Electronica 3 | Attribution-Noncommercial-Share Alike 3.0 United States |
| 116456 | Montana Skies | Canyon Breeze | Attribution-Noncommercial-Share Alike 3.0 United States |
| 116514 | SULCUS | R.A.H.M.A.V.N. [221] | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 116709 | Jaan Patterson | Gnossiennes No.1 (Éric Alfred Leslie Satie) | Public Domain Mark 1.0 |
| 116869 | Blue Dot Sessions | Ray Gun - Lull | Attribution-NonCommercial |
| 116883 | Joan Shelley | Something Small | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 117287 | My brother Daniel | Wee Bird | Attribution-NoDerivatives 4.0 International |
| 117628 | Julia K. Mars | Cool Blues | Attribution-NonCommercial-ShareAlike |
| 117885 | Connections | Cul de Sac/Tough City | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 118000 | Juan Angel Italiano | Anthony Donovan, Marcel Duchamp, Georges Bataille | Attribution-NonCommercial-ShareAlike 3.0 International |
| 118195 | PC-ONE | Ave | Attribution |
| 118496 | Dan Cantrell | 2 | Attribution |
| 118671 | Red Trees | Butterfly Shield | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 118950 | Igor Khabarov | Fall | Attribution-NonCommercial 3.0 International |
| 119026 | Change! | Colourdome | Attribution-NonCommercial |
| 119413 | Eddie The Rat | Waiting For Enlightenment Blues | Attribution-NonCommercial |
| 119715 | Kraus | Side 2 | Attribution-Noncommercial-Share Alike 3.0 New Zealand |
| 119897 | Blue Dot Sessions | Jefferson | Attribution-NonCommercial |
| 119988 | Cassette Tape Bandits | Spot Rockers | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 120171 | mr_melvis | One Minute To Midnight (Or: How I Spent New Year's Eve 2005) | Attribution-NonCommercial |
| 120201 | R. Stevie Moore (with Irwin Chusid) | So Be It Soviet, The Undertow | Attribution-NonCommercial |
| 120305 | Cory Gray | Doctor 2 | Attribution-NonCommercial 3.0 International |
| 120319 | Cory Gray | One Shot Charlie | Attribution-NonCommercial 3.0 International |
| 120332 | Cory Gray | WTS | Attribution-NonCommercial 3.0 International |
| 120518 | Kotúče DM | Zaczek | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 121251 | Guerra de Cerdos | El Horror Sucesivo del Vacío | Attribution-NoDerivatives 4.0 International |
| 121320 | Coin Locker Kid | goldheart | Attribution |
| 121472 | Satellite Ensemble | Archival (Emergence) | Attribution-NonCommercial 3.0 International |
| 121652 | happiness in aeroplanes | The Silent World | Attribution-NonCommercial-ShareAlike |
| 121665 | happiness in aeroplanes | Wayward Docks | Attribution-NonCommercial-ShareAlike |
| 121913 | Negritage | Dubbing into di wild | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 122066 | Jahzzar | A Message | Attribution-ShareAlike |
| 122107 | Electroblurgen | A Cheetah Grimly Grooms | Attribution-NonCommercial |
| 122352 | David Mumford | Lover | Attribution |
| 122395 | Gorilla Chief | Intro | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 122500 | Elisa Luu | "Outsider girl" | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 122624 | A Ninja Slob Drew Me | We Are; | Attribution-NonCommercial |
| 122646 | Josh Spacek | Little Strings | Attribution-NonCommercial 3.0 International |
| 122798 | The Carnys | singaling | Attribution-Noncommercial-Share Alike 3.0 United States |
| 122910 | ODOR BABY | GOODNIGHT ZUKI | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 123094 | Joanna Sternberg | A Country Dance | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 123441 | The Dread | Thrashin | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 123494 | David Mumford | The Right Train Meet / Meet Me Tomorrow | Attribution |
| 123760 | Hawkin | Woods | Attribution-NonCommercial |
| 123899 | Rachel Mason | Rebel Angels | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 123970 | BenJamin Banger | Agoge | Attribution |
| 124154 | Kosta T | не осуждайте Гитлера (он болен) ... | Attribution |
| 124231 | ODOR BABY | AVIATION CRAB ORCHESTRA (PACHELBELS CANNON) | Attribution |
| 124474 | Attic Ted | Funny Ways | Attribution-Noncommercial-Share Alike 3.0 United States |
| 124554 | Uno Datwangi | The Need | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 124876 | Sergey Cheremisinov | Fragile Ice | Attribution-NonCommercial |
| 125001 | Let Me Crazy | 899 Seconds | Attribution-Noncommercial-Share Alike 3.0 United States |
| 125185 | Derek Clegg | Life Unfolds | Attribution-NonCommercial-ShareAlike |
| 125238 | Derek Baron | Palmillas | Attribution-NonCommercial |
| 125314 | Dengue Fever | Tokay | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 125620 | Blue Dot Sessions | Likin | Attribution-NonCommercial |
| 125778 | Atlantic Thrills | Weekend | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 125822 | Blue Dot Sessions | Slimm | Attribution-NonCommercial |
| 126055 | Dolores Catherino | Temporal Parallax | Attribution-ShareAlike |
| 126189 | Joao Picoito | Joy | Attribution-NonCommercial-ShareAlike |
| 126227 | Ø+yn + GMC | La Barcaza | Attribution-ShareAlike |
| 126289 | Blue Dot Sessions | Thread Caramb | Attribution-NonCommercial |
| 126321 | Janneh | Kiitos | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 126408 | Blue Dot Sessions | A Simple Shroud | Attribution-NonCommercial |
| 126428 | Shy and the Fight | Here the sea speaks in whispers | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 126512 | Ophed | Pythian Chant | Attribution-NonCommercial-ShareAlike 3.0 International |
| 126588 | Maxim Kornyshev | Look Out The Window At The Rain | Attribution-ShareAlike |
| 126667 | Sleep Out | Halloween Parade | Attribution-NonCommercial 3.0 International |
| 126719 | Blue Dot Sessions | Cottonwoods | Attribution-NonCommercial |
| 126790 | Connie Acher & Blind Drunk John | Golden Success | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 127062 | Robin Mitchell | When I'm Gone | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 127189 | Wintermitts | A La Fraîche | Attribution-NonCommercial |
| 127210 | Satori Ensemble | The Chamber | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 127276 | Hissing Fauna | Shin A-lam | Attribution-NonCommercial-ShareAlike |
| 127295 | Oh Yeah, the Future | Hungry | Attribution |
| 127349 | Lately Kind of Yeah | P.Mantium | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 127496 | Blue Dot Sessions | Entrap | Attribution-NonCommercial |
| 127660 | Blue Dot Sessions | Mknt | Attribution-NonCommercial |
| 127868 | Dana Boulé | Clichy Waltz | Attribution-NonCommercial |
| 127916 | Stephen Chopek | Hurry | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 128482 | Solar Estates | Wild Waste | Attribution-NonCommercial-ShareAlike |
| 128759 | Synapsis | Atrusion Fusion | Attribution |
| 128845 | Jaan Patterson | XI | Attribution-NonCommercial-ShareAlike 3.0 International |
| 129040 | Blue Dot Sessions | Slow Strutt | Attribution-NonCommercial |
| 129088 | The Vivisectors | Vodka And Beer | Attribution-NonCommercial-ShareAlike |
| 129186 | Blue Dot Sessions | Paper Napkin | Attribution-NonCommercial |
| 129395 | Jerry Harris | Spreading All Over | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 129439 | Bridget St John | The Hole In Your Heart | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 129806 | Captive Portal | Oak Bloke | Attribution-Noncommercial-Share Alike 3.0 United States |
| 129886 | Tzelalot | WhoShall I Lah | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 129925 | Radio Jarocho and Zenen Zeferino Huervo | Morena | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 130130 | Michett | Atmo | Attribution |
| 130349 | Goddess | Paradise | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 130683 | The Zombie Dandies | The Friendly Monster Shop | Attribution |
| 130922 | mathgrant | A Jubilant Sport | Attribution-Noncommercial-Share Alike 3.0 United States |
| 130948 | Albin Andersson | A Penny For Your Dreams | Attribution-NonCommercial-ShareAlike |
| 130990 | Blue Dot Sessions | Between Stones | Attribution-NonCommercial |
| 131325 | Blue Dot Sessions | Rally | Attribution-NonCommercial |
| 131449 | Mom Jeans | B03 John Waters | Attribution-Noncommercial-Share Alike 3.0 United States |
| 131656 | The Flamin' Groovies | Shake Some Action | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 131792 | Pranavibes | Invitation | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 131911 | David Szesztay | Above Us | Attribution-NonCommercial 3.0 International |
| 131934 | Edessa | Sheikhane | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 131979 | Nafta | Ti (garage exclusive) | Attribution-NonCommercial-ShareAlike 3.0 International |
| 132118 | Humble | Just Like That | Attribution-NonCommercial-ShareAlike |
| 132434 | Tea Man With Tea Gum | Oh, My Cat! | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 132675 | Future Sauce | When I am King | Attribution-NonCommercial-ShareAlike |
| 132788 | Gardenella | Squirrels | Attribution-Noncommercial-Share Alike 3.0 United States |
| 132964 | Cockmasta Flex | Deeper (remastered) | Attribution-NonCommercial-ShareAlike |
| 133029 | SPCZ | Pulse | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 133431 | TRG Banks | David Hemmings | CC0 1.0 Universal |
| 133444 | TRG Banks | Rainbow | CC0 1.0 Universal |
| 133457 | TRG Banks | Fresh pastures | CC0 1.0 Universal |
| 133562 | David Szesztay | Prayer | Attribution-NonCommercial 3.0 International |
| 133639 | Bonnie 'Prince' Billy | 66 | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 133833 | gunctrl | Spare Succulence | Attribution-NonCommercial-NoDerivatives (aka Music Sharing) 3.0 International |
| 133972 | Marcos H. Bolanos | To Distant Lands | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 134084 | Windmill | An ordinary man | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 134449 | TRG Banks | Through the asteroid belt | CC0 1.0 Universal |
| 134643 | ODOR BABY | AVIATION CRAB ORCHESTRA (BUILD A WALL) | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 134826 | Tha Silent Partner | GoodLookN | Attribution-NonCommercial-ShareAlike |
| 134935 | Turku, Nomads of the Silk Road | -Yesilim | Attribution |
| 134951 | Turku, Nomads of the Silk Road | -Antalyanin Mor Uzumu | Attribution-Noncommercial-Share Alike 3.0 United States |
| 135089 | Akpatok | You Washed Your Face in a Mountain Stream | Attribution-Noncommercial 3.0 United States |
| 135229 | Ehl-i Keyif | Hüzzam Kanun Taksim | Attribution-NonCommercial-ShareAlike |
| 135370 | Lamprey | Happy | Attribution |
| 136137 | Philippa Dowding and Allister Thompson | Spring | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 136465 | C-Doc | TheEnsuranceTrap featuring Tab (PMRC Mix) | Attribution-NonCommercial-ShareAlike |
| 137054 | S-B-J | Oh My God | Attribution |
| 137176 | Ars Sonor | Remember (We Are Divine, We Are Immortal) | Attribution-ShareAlike |
| 137462 | beat.dowsing | gurgle | Attribution-Noncommercial-Share Alike 3.0 United States |
| 137631 | The Sun Through A Telescope | It's Cold But I'm Warm | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 137895 | C-Doc | The Rebel featuring Cheese N Pot-C | Attribution-NonCommercial-ShareAlike |
| 138017 | Pierce | Sober Trip | Attribution-NonCommercial |
| 138041 | Massage | Lydia | Attribution-NonCommercial |
| 138071 | Black Flamingo | Black Bird | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 138311 | Big White | Down At The Beach | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 138578 | harmonic defiance | the endless corner | Attribution-Noncommercial-Share Alike 3.0 United States |
| 139461 | Dlina Volny | Volny Dlina | Attribution-NonCommercial-ShareAlike |
| 139638 | aboombong | arabesque | Attribution-Noncommercial-Share Alike 3.0 United States |
| 139777 | Evgeny Teilor | Runaway | Attribution-NonCommercial |
| 139993 | Resonancedj | The Cavern | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 140316 | Miquel Parera & Computer | nxI2016-07-12_00_20_40-A-2016-07-12_04_00_24 | Attribution-Noncommercial-Share Alike 3.0 United States |
| 140625 | The Impossebulls | Back To It (12inchstrumental) | Attribution-NonCommercial-ShareAlike |
| 140923 | Nihilore | Whispers Invoke Paranoia | Creative Commons Attribution |
| 141144 | Andy G. Cohen | Land Legs | Attribution |
| 141284 | Kevin Hartnell | Tree of Tears | Attribution-ShareAlike |
| 141299 | Kevin Hartnell | Interlude #13 | Attribution-ShareAlike |
| 141590 | Mikuś | Dub Mirror | Attribution |
| 141876 | Andy G. Cohen | Warmer | Attribution |
| 142083 | SK | Still Living | Attribution |
| 142098 | TRG Banks | Lake in the woods | CC0 1.0 Universal |
| 142433 | TRG Banks | Above Birmingham | CC0 1.0 Universal |
| 142536 | Terratombats | El iaio | Attribution-Noncommercial-Share Alike 3.0 United States |
| 142567 | GOYMAMBA | Fell it | Attribution-Noncommercial-Share Alike 3.0 United States |
| 142666 | J1* | Floridian Fighting Bear | Attribution |
| 143056 | Mystery Mammal | Speeding Speech | Attribution |
| 143215 | The Invasion | Halfway To Infinity | Attribution-NonCommercial 3.0 International |
| 143300 | Chase Alan Willis | Cotton Candy Monster | Attribution-NonCommercial-ShareAlike |
| 143995 | Hurry Up | Kick Em Out | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 144216 | The Impossebulls | Let Me Spell You This (016 Tess Dis Champion Mixx) | Attribution-NonCommercial-ShareAlike |
| 144474 | Nick Jaina | Telepathic Teleplay (Instrumental) | Attribution-NonCommercial 3.0 International |
| 144549 | Wildlight | Ours To Give (Acoustic) | Attribution-Noncommercial-Share Alike 3.0 United States |
| 144933 | Maxwell Powers | Saturday | Attribution-NonCommercial-ShareAlike |
| 144946 | Maxwell Powers | After All You Say (TSPMIX) | Attribution-NonCommercial-ShareAlike |
| 145068 | TxSxBx | Demoni e Dei (Contropotere) | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 145458 | Squire Tuck | Dilemma of the Heart | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 145556 | This Mess is Mine | Roommates (Piano Version by AGPB) | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 145706 | Maxwell Powers | Saturday (Weekend Groove Remix) | Attribution-NonCommercial-ShareAlike |
| 145743 | Cheese N Pot-C | Snow Crystal Whispering Pines (Featuring HighSnow) | Attribution-NonCommercial-ShareAlike |
| 145757 | SPCZ | Tube | Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 |
| 146018 | Ars Sonor & Malga Kubiak | Världens Ände | Attribution |
| 146478 | Norwegian Blue Records | SPARKLE BLOOD - Denim Convention | Attribution-NonCommercial |
| 146725 | Regenerated Headpiece | Dogfight (Live) | Attribution-NonCommercial-ShareAlike |
