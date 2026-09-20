# WARDOGS game mechanics research

Researched September 19, 2026. This delivery targets the game's **L81 mortar**. The implementation is a coordinate-based companion, not a terrain map or physics simulator.

## Visual reference

The updated design takes inspiration from the relationship between a physical object and its alignment guidance in the [Starlink alignment experience](https://starlink.com/videos/9), also described in its [official installation guide](https://starlink.com/public-files/installation_guide_standard4_kit.pdf). The application uses original procedural WPF geometry, a restrained dark palette, and direct camera controls. No Starlink artwork, branding, or models are included.

## Primary implementation source

[Apollyon's open-source WARDOGS calculator](https://github.com/apollyon-sys/wardogs-calculator) provides inspectable game-specific coordinate logic, map calibration, and community weapon tables under MIT.

Pinned snapshot: `6a9fcd9afcea4254df59dfba4df5cc2627a92112`.

| Finding | Evidence | Application behavior |
| --- | --- | --- |
| Each coordinate unit represents 100 game meters on the three maps | [Bakurani](https://github.com/apollyon-sys/wardogs-calculator/blob/6a9fcd9afcea4254df59dfba4df5cc2627a92112/maps/bakurani.json), [Ozeti](https://github.com/apollyon-sys/wardogs-calculator/blob/6a9fcd9afcea4254df59dfba4df5cc2627a92112/maps/ozeti.json), [Zestafona](https://github.com/apollyon-sys/wardogs-calculator/blob/6a9fcd9afcea4254df59dfba4df5cc2627a92112/maps/zestafona.json): `coordinateMetersPerUnit` | Euclidean grid distance multiplied by 100 |
| Positive X points east and positive Y points north | [results.js](https://github.com/apollyon-sys/wardogs-calculator/blob/6a9fcd9afcea4254df59dfba4df5cc2627a92112/js/features/results.js): `atan2(dx, dy)` | Bearing clockwise from north, normalized to 0–360° |
| L81 supported range is 132–684 m | [weapons.json](https://github.com/apollyon-sys/wardogs-calculator/blob/6a9fcd9afcea4254df59dfba4df5cc2627a92112/data/weapons.json) | Range checks use unrounded distance |
| L81 elevation samples are game-specific | Same weapon file, mortar `ballistics.single` | Retain only samples within the playable interval; interpolate between adjacent samples |

The raw snapshot files are in `research/`. `src/l81.csv` contains only L81 rows within the declared range. No real-world firing tables or ballistic physics are used. Other weapons and their tables are not exposed by the app.

## In-game interface and coordinate acquisition

[A player's first-hand explanation](https://www.reddit.com/r/OfficialWARDOGS/comments/1w8yf36/spotters_needed/) describes the map's spotted-object range display and the mortar's left-side range gauge, with a target ping for direction. The app therefore prioritizes **bearing and RNG**, with MIL shown as an estimate.

[A community calculator author's post](https://www.reddit.com/r/WarDogs/comments/1wigims/simple_mortar_calculator_distance_azimuth/) documents copied coordinate strings such as `x99.44, y44.65`. Its embedded bearing code uses the opposite Y sign, so this app follows the calibrated open-source project above instead. Direction tests explicitly cover all eight main compass directions.

[The map-to-coordinate workflow and worked example](https://metacounters.com/wardogs/news/how-to-land-mortar-shots-first-try) describe right-click → Mark Coordinates → copy the pair from chat and the example used by this app. This is a secondary description of SwoleBenji's game footage, used for workflow context and a cross-check of the distance.

The precise mount, aim, and traverse key bindings may vary with remapping and updates. The app's guide asks the player to use the current game sight and map ping, rather than treating a historical binding as mandatory.

## Confidence and limits

The grid convention and table values are verified against the pinned community source, not an official BULKHEAD specification. In-game behavior was not measured by firing rounds during development. The calculator does not account for terrain elevation, obstructions, or future balance patches. A manual distance replaces calculated grid range without changing heading; the UI always identifies this mode.

The deployed app performs no online research or data refresh. To update the L81 table, replace the in-range CSV with a newly verified game-data snapshot, update this provenance, rebuild, and run tests.
