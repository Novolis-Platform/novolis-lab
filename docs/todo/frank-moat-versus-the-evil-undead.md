# FRANK MOAT vs. THE EVIL UNDEAD

**Genre:** 2D top-down action shooter / splatter shooter  
**Perspective:** Fixed-orientation top-down 2D  
**Platform:** Windows first, Linux viable  
**Technology:** .NET 10 / C# 14 / Novolis / Silk  
**Player count:** Single-player initially  
**Campaign:** Level 0 → Level 69 → Level 68 → … → Level 1 → Level 0  
**Tone:** Gun-nerd workplace satire interrupted by industrial-scale undead violence

---

# 1. High concept

Frank Moat is a retired United States Marine Corps Gunnery Sergeant and former Battalion Mess Chief.

After retiring, he deliberately selected an exceptionally boring second career: weekday lobby security at a vast privately owned subterranean research facility.

He likes the company.

The founder was an obsessive firearms enthusiast, and that enthusiasm calcified into corporate culture. Gun ranges are treated approximately the way technology companies treat foosball tables. Departmental shooting clubs are normal. Ammunition subsidies are a benefit. Historical firearms decorate offices. Engineering has a competitive rotary-gun league that HR insists on calling something else.

Frank fits in beautifully.

At the beginning of an otherwise unremarkable morning, the facility suffers a catastrophic containment event.

The facility AI invokes:

## Emergency Containment Directive 4761

`4761 = 69²`

All external exits lock.

Almost everyone below Level 0 stops responding.

FAITH, the facility AI, determines that Frank is:

1. alive,
2. on Level 0,
3. currently the only available employee with unrestricted weapons authorization,
4. a retired Gunnery Sergeant,
5. therefore the nearest thing the emergency plan has to a solution.

Frank protests that he was a cook.

FAITH correctly observes that he was a **Gunnery Sergeant who happened to run food service**, not a man whose only qualification was making omelettes.

The emergency protocol permits movement deeper into containment, but not evacuation.

Fortunately, the deceased founder had constructed a private express elevator connecting the lobby directly to **Level 69**.

Level 69 contains his preserved office, museum, private range, emergency bunker, historical weapons collection and prototype vault.

Frank goes down.

He gets kitted out.

The founder's elevator then locks under Directive 4761.

Frank must clear the facility **one floor at a time on the way back up**.

His ultimate objective is wonderfully modest:

> Get back to his security post on Level 0 and go home.

---

# 2. Design pillars

## 2.1 Movement must feel excellent before anything else exists

Frank is extremely responsive.

There is little acceleration and almost no deceleration lag. Press a direction and he moves. Release it and he stops.

There is no combat roll.

There is no cover snapping.

There is no animation-priority system that prevents the player from responding because Frank has committed himself to a beautiful 1.4-second animation.

The player is controlling Frank, not politely suggesting actions to him.

---

## 2.2 Guns are characters

Weapons are not `Damage = 12` with different sprites.

The gun itself matters.

Real firearms should be recognizable by operation, sound, reload behaviour, capacity and personality.

A 1911 behaves like a 1911.

A Winchester lever gun loads and cycles like a lever gun.

A tube-fed shotgun can be topped up between engagements and interrupted halfway through loading.

The fictional weapons receive the same attention.

Frank knows this stuff and is excited by it.

Weapons should create moments where the player voluntarily stops moving simply to inspect what they have found.

---

## 2.3 Violence is excessive, readable and systemic

Combat should leave evidence.

A pristine room before combat and the same room thirty seconds later should look meaningfully different.

Shots produce:

- directional blood spray,
- floor and wall decals,
- impact debris,
- sparks,
- shell cases,
- smoke,
- fragments,
- dismemberment where appropriate,
- physical corpse reactions,
- broken props,
- scorch marks,
- persistent gore.

This is spectacle, but it is also feedback.

The player should understand what a weapon did by looking at the result.

---

## 2.4 Levels are places, not arenas

Combat happens inside coherent facility floors.

Rooms have purposes.

Corridors reconnect.

Maintenance routes create shortcuts.

Locked doors seen early may become accessible twenty minutes later.

The player gradually develops a mental model of each floor.

A level should repeatedly produce:

> “Wait. I'm back here.”

---

## 2.5 The game is self-aware without breaking its universe

Frank does not know he is in a video game.

FAITH does not know she is narrating one.

They do, however, recognize when the facility is behaving absurdly.

Three ammunition crates outside an enormous locked chamber are suspicious in-universe too.

Frank notices chest-high concrete partitions.

FAITH calls them “acoustic barriers.”

Frank notices that every explosive drum is red.

FAITH explains the corporate safety rationale.

The player and characters share the joke without anyone talking about polygons, save points or boss fights.

---

# 3. Camera and visual presentation

The camera is close enough that enemies remain characters rather than colored ants.

Recommended baseline:

- fixed world orientation,
- no camera rotation during normal play,
- orthographic 2D projection,
- 16:9 viewport,
- Frank approximately 5-8% of screen height,
- moderate look-ahead toward aim direction,
- small user-controlled zoom range.

The fixed orientation is important because levels should become mentally navigable.

## Rendering style

The world is true 2D rather than a 3D game viewed from above.

Characters use directional animation with a separately aimable weapon layer where useful.

The game can use:

- high-resolution sprites,
- normal maps where appropriate,
- dynamic 2D lights,
- particle lighting,
- animated environmental machinery,
- shadows as readability aids.

Visual hierarchy:

**Walls and floor:** restrained industrial palette  
**Enemies:** strong silhouettes  
**Weapons/fire:** bright  
**Interactables:** immediately legible  
**Blood:** unapologetically visible

The screen may become disgusting without becoming visually confusing.

---

# 4. Core controls

Controls are designed around independent movement and aiming.

The player must be able to run north while firing west without entering a special stance.

## Keyboard and mouse

| Input | Action |
|---|---|
| `WASD` | Move |
| Mouse | Aim |
| Left mouse | Primary fire |
| Right mouse | Secondary fire / weapon-specific alternate |
| `Shift` | Run |
| `R` | Reload |
| `E` | Interact / use |
| `F` | Quick melee / shove |
| `Q` | Previous weapon |
| Mouse wheel | Cycle weapons |
| `1-6` | Weapon category / quick slot |
| Hold `Tab` | Floor map |
| `Z / X` | Camera zoom out / in |
| `I` | Inspect weapon |
| `Esc` | Pause |
| Hold weapon-selection key | Weapon wheel |

No stamina bar is attached to running.

Frank was a Marine and this is an action game, not a cardiology simulator.

Running may modestly increase dispersion on weapons for which that matters, but the game never demands walking simply because an energy meter ran out.

## Controller

| Input | Action |
|---|---|
| Left stick | Move |
| Right stick | Aim |
| Right trigger | Primary fire |
| Left trigger | Secondary fire |
| Left bumper | Run |
| Right bumper | Quick melee / shove |
| `A` | Interact |
| `X` | Reload |
| Tap `Y` | Previous weapon |
| Hold `Y` | Weapon wheel |
| D-pad | Direct weapon groups |
| View | Map |
| Right-stick click | Inspect weapon |
| Menu | Pause |

Controller aiming receives configurable friction and very light magnetism near hostile silhouettes.

It does **not** automatically rotate Frank onto targets.

All controls are remappable.

---

# 5. Movement

Frank has two movement speeds:

**Move:** controlled navigation and accurate fire.

**Run:** fast movement, unlimited, capable of firing simultaneously.

There is no dodge roll.

There may eventually be extremely short contextual evasive movement caused by explosions or impacts, but no universal invulnerability button.

## Collision

Frank uses a compact collision footprint slightly smaller than his sprite.

This makes doorways and densely furnished rooms feel fair.

Characters may slide smoothly along walls rather than becoming stuck on corners.

Enemy bodies do not permanently block traversal after death.

Large living enemies absolutely can.

---

# 6. Aiming and shooting

The weapon points toward the cursor/right stick.

Frank's torso and feet may face different directions during strafing.

This recreates the important old top-down-shooter behaviour of moving independently from the direction of fire without requiring a separate “strafe mode.”

## Conventional firearms

Most conventional bullets are simulated as immediate ballistic traces because combat distances are short.

The trace can still model:

- penetration,
- material resistance,
- multiple targets,
- ricochet where plausible,
- impact direction,
- hit location.

The bullet itself does not need to crawl visibly across the room.

## Visible projectiles

These include:

- grenades,
- rockets,
- launched explosives,
- experimental plasma-like systems,
- slow research projectiles,
- thrown objects.

They can collide with enemies and environment geometry.

## Energy weapons

Beam weapons such as the Fazer produce actual sustained or pulsed beams, with heat and surface effects.

---

# 7. Damage model

Gameplay damage and visual gore are deliberately separate systems.

A hit produces a gameplay event approximately equivalent to:

`DamageEvent(Target, Weapon, Energy, Point, Direction, DamageType)`

Gameplay resolves:

- health,
- armor,
- stagger,
- penetration,
- status,
- death,
- optional dismemberment state.

The presentation layer then turns the result into irresponsible quantities of visual feedback.

This means reducing gore in accessibility settings never alters game balance.

---

# 8. Gore system

This is one of the game's technical showpieces.

## Immediate effects

A sufficiently violent impact can emit:

- blood mist,
- large droplets,
- fine droplets,
- tissue particles,
- bone fragments,
- equipment fragments,
- clothing fragments,
- sparks from cybernetic or mechanical enemies,
- smoke,
- projectile debris.

Direction and energy come from the damage event.

A shotgun fired east should make the result visibly travel east.

## Persistent evidence

Blood decals remain on:

- floors,
- walls,
- furniture,
- doors,
- machinery.

Bodies remain where practical.

Detached chunks can become low-cost environmental objects after their active physics period ends.

## Performance strategy

Gore has several lifetimes.

**Immediate particles:** thousands, extremely cheap, short lived.

**Physical fragments:** tens/hundreds, pooled.

**Decals:** persisted until budget pressure.

**Historical blood:** old decals are flattened into a per-level blood/stain texture rather than kept as individual entities.

This allows a level to remember an hour of violence without maintaining twenty thousand live decal objects.

## Gore settings

- Off
- Reduced
- Full
- Founder Approved

Founder Approved is the intended presentation.

---

# 9. Weapons

There are no rarity colors.

There is no `Legendary +7% Critical Damage 1911`.

A gun is valuable because of what gun it is.

## Ammunition

Ammo is cartridge-based.

Examples:

- `.45 ACP`
- `9×19`
- `.357 Magnum`
- `.44 Magnum`
- `.45-70`
- `5.56`
- `7.62`
- `12 gauge`
- fictional power cells
- fictional specialist ammunition

Multiple firearms can therefore compete for the same reserve.

The player does not manually inventory individual magazines.

However, weapons retain meaningful mechanical state:

- current magazine/tube/cylinder,
- chambered round where relevant,
- reserve ammunition.

Partial reloads do not throw ammunition into the void.

## Example conventional weapons

### M1911

Frank's emotional support pistol.

Reliable, powerful, limited capacity, crisp.

Frank notices differences between examples he finds.

### Winchester Model 1894 family

Powerful lever gun.

Round-by-round reload.

Excellent penetration depending on chambering.

Completely unnecessary corporate-office weapon.

Naturally popular in Facilities.

### Pump shotgun

Close-range room rearrangement device.

Individual shell loading.

Can interrupt loading and fire.

Massive physical feedback.

### MP5 family

Extremely controllable 9mm automatic weapon.

Frank approves.

### Revolvers

Mechanically distinct rather than merely “pistol but six rounds.”

### AR-pattern rifles and other recognizable service weapons

Common enough to support the corporate shooting culture and security teams.

Actual commercial product names receive a trademark/licensing review before release. Mechanics do not depend on using protected branding.

---

# 10. Fictional weapons

Fictional weapons may deliberately evoke broad science-fiction firearm traditions but must have original names, silhouettes, sounds and operation.

## Fazer Mk LXIX

The signature weapon.

**Fazer = Fostered/Focused Advanced Zenith Energy Rifle**, or another suitably tortured corporate backronym once company naming is final.

It looks like an optimistic future designer was asked to build a practical infantry laser rifle and given irresponsible funding.

Modes:

**Pulse:** fast precise energy shots.

**Sustained:** continuous beam, enormous heat accumulation.

Properties:

- almost no recoil,
- extremely accurate,
- power-cell ammunition,
- surface heating,
- armor interaction,
- visually magnificent,
- can cut through multiple soft targets,
- sustained use overheats focusing components.

Frank sees one on Level 69 before he is allowed to possess it.

This becomes a running grievance.

Later he earns/accesses it.

His reaction should justify the entire weapon-inspection feature.

## Other prototypes

- electromagnetic penetrator,
- industrial arc projector,
- rotary flechette platform,
- experimental thermal cutter,
- acoustic impulse weapon,
- engineering's appallingly modified rotary cannon.

---

# 11. Weapon inspection

Pressing Inspect pauses hostile simulation and presents the current firearm prominently.

Frank turns/examines it.

The player receives:

- name,
- chambering/power source,
- operation,
- fire modes,
- current condition if relevant,
- short technical description.

Frank comments.

FAITH may respond.

These are optional 5-20 second character moments.

Inspection is not a stat spreadsheet.

For real firearms, Frank's remarks emphasize recognizable mechanical details.

For prototypes, his enthusiasm becomes increasingly childlike.

---

# 12. Company gun culture

Firearms enthusiasm is institutional culture.

The founder believed responsible firearms ownership demonstrated:

- mechanical curiosity,
- discipline,
- attention to detail,
- confidence under pressure.

HR eventually translated this into sanitized language such as:

> “Mechanical confidence and precision-sport cultural alignment.”

Everybody knows what it means.

## Employee perks

Typical benefits include:

- free range membership,
- subsidized ammunition,
- departmental competitions,
- historical-firearms club,
- company marksmanship events,
- firearms training,
- employee storage lockers.

Gun ranges are treated culturally like tech-company foosball tables.

Nobody inside the company thinks this is remarkable.

## Departmental character

**Security:** practical shooting and duty weapons.

**Accounting:** frighteningly serious pistol competitors.

**Facilities:** shotguns, lever actions, revolvers.

**Legal:** expensive firearms in suspiciously pristine condition.

**Executives:** commemorative pistols and presentation rifles.

**Engineering:** High-Rate Rotary Systems Practical Division.

Everyone else calls it the **minigun league**.

Engineering disputes the terminology every year.

This culture justifies ammunition and firearms appearing throughout the facility without resorting to floating videogame pickups.

Ammo comes from:

- range lockers,
- personal lockers,
- departmental safes,
- competition gear,
- security posts,
- storage,
- abandoned range bags,
- shipping crates,
- prototype labs.

Environmental placement tells jokes and stories.

---

# 13. Level 0 opening

The first 5-10 minutes contain no combat.

Frank works the lobby.

The sequence quietly teaches:

- movement,
- interaction,
- dialogue,
- looking/aiming without a firearm,
- map use,
- facility layout.

Employees arrive.

Someone forgets a badge.

A courier has the wrong loading-dock number.

FAITH gives routine announcements.

Frank and FAITH clearly already know one another professionally.

Then something interrupts her.

Lights shift.

Elevators stop.

External shutters close.

`DIRECTIVE 4761 ACTIVE`

FAITH attempts to contact emergency teams.

Most fail.

She reviews Level 0 personnel.

Frank is the only person with suitable clearance and access.

Frank:

> “I was a cook.”

FAITH:

> “You retired as a Gunnery Sergeant.”

Frank:

> “A very senior cook.”

FAITH assigns him the incident.

---

# 14. Level 69

Frank's first destination is not a battlefield.

The founder's private elevator travels directly from 0 to 69 because of course it does.

Level 69 is untouched.

It has isolated:

- power,
- air,
- security,
- communications,
- fire suppression.

It is a preserved shrine to the founder.

There are **no enemies**.

It contains:

- private office,
- memorial gallery,
- historical gun collection,
- indoor range,
- prototype vault,
- bunker,
- absurd private bar,
- company-history exhibits,
- recorded founder presentations.

This is the game's first major exposition block and player's initial arsenal selection.

The player should spend as long as they like wandering around.

Then FAITH explains that Directive 4761 prohibits the founder elevator from returning personnel toward external egress until the intervening containment zones are certified.

Frank must take the service route to Level 68.

And then 67.

And so on.

---

# 15. Campaign geography

The facility uses underground level numbering:

`0` = ground/lobby.

`1` = first underground level.

`69` = deepest numbered level.

Therefore Frank travels:

`0 ↓ 69 ↑ 68 ↑ 67 ... ↑ 2 ↑ 1 ↑ 0`

The entire game is a circular journey back to where his morning began.

## 69 floors does not mean 69 enormous maps

There are three floor scales.

### Transit floors

5-10 minutes.

Focused theme, several fights, a small objective.

### Standard floors

10-20 minutes.

Exploration, loops, secrets, multiple combat events.

### Anchor floors

25-45 minutes.

Major narrative or gameplay floors with unique mechanics, bosses or large structural puzzles.

Approximately 8-10 floors are anchors.

This targets roughly 12-18 hours for a first playthrough without abandoning the sacred number.

---

# 16. Backtracking

The player may travel back toward Level 69 whenever physical access allows.

There is **no global fast travel**.

If Frank is on 31 and wants something he left on 52, he walks there.

The world is continuous enough for this to matter psychologically.

Cleared floors remain mostly cleared.

They may contain occasional stragglers or newly opened environmental hazards, but enemies do not magically respawn simply to punish backtracking.

The inconvenience is distance.

This makes caches, weapon choices and ammunition planning matter without turning the game into survival horror.

---

# 17. Level design

Each floor has:

- a recognizable workplace function,
- a central traversal problem,
- several interconnected routes,
- at least one shortcut,
- secrets,
- optional rooms,
- one or more containment objectives.

Typical progression:

Explore  
→ encounter resistance  
→ find blocked route  
→ discover alternate path  
→ obtain access/restore system  
→ open old route  
→ recognize earlier location  
→ complete containment  
→ access next floor

Doors are not purely color-coded.

Access uses visually distinguishable:

- authorization levels,
- departments,
- mechanical locks,
- emergency overrides,
- physical damage.

Any color coding also uses shape/text so accessibility does not depend on hue.

---

# 18. Combat spaces

The game avoids repeated “enter rectangle, doors lock, kill waves” design.

Some lock-ins exist because containment doors are part of the setting.

But most combat begins organically:

- opening doors,
- entering occupied rooms,
- waking dormant enemies,
- making noise,
- triggering machinery,
- accidentally releasing containment,
- encountering roaming hostiles.

The player can often retreat.

Enemies can pursue through connected spaces.

---

# 19. Noise

Weapons create noise events.

Noise propagates through the level graph based on:

- distance,
- doors,
- walls,
- machinery,
- current alarms,
- weapon intensity.

A suppressed pistol may alert the next office.

A shotgun alerts several rooms.

Engineering's rotary gun informs **the entire postcode**.

Enemies can:

- investigate noise,
- approach cautiously,
- rush,
- trigger other enemies,
- enter combat already searching.

This lets the player create their own disasters.

---

# 20. Enemy behaviour

Enemies must be readable quickly.

The player should identify an enemy's function primarily from movement and silhouette.

Baseline roles:

### Shambler

Slow mass pressure.

Weak individually.

Excellent gore substrate.

### Runner

Fast and fragile.

Forces target prioritization.

### Heavy

Large, slow, absorbs fire, physically controls space.

### Crawler

Uses maintenance passages and small routes.

### Reclaimer

Attempts to revive or reassemble recently killed biological enemies.

### Contaminated Security

Uses weapons imperfectly and understands doors.

### Failed Prototype

Unpredictable experimental organism.

### Automated Defense Unit

Not undead, merely still following terrible orders.

### Composite

Multiple organisms and facility equipment assembled into something that should not exist.

Enemy attacks must support friendly fire where sensible.

Hostiles can hurt each other.

Chaos is a feature.

---

# 21. AI philosophy

Enemies know only what they can reasonably perceive.

They respond to:

- sight,
- sound,
- damage,
- nearby allies,
- alarms,
- environmental triggers.

No omniscient player coordinates.

The AI should produce explainable stupidity and explainable competence.

This is more valuable than perfect tactical play.

---

# 22. Narrative system

There are three voices.

## Frank

Human reaction.

Gun nerd.

Dry.

Experienced administrator.

Not invulnerable action-movie bravado.

## FAITH

Facility AI.

Operational narrator.

Helpful.

Literal without being stupid.

Increasingly conversational as the incident continues.

She is not secretly evil.

## The Founder

Dead for decades.

Only appears through recordings, documents and facility design.

He was:

- technically literate,
- absurdly wealthy,
- obsessed with firearms,
- institutionally juvenile,
- distrustful of empty bureaucracy,
- reckless,
- frequently ridiculous,
- occasionally infuriatingly correct.

His personality should feel original rather than impersonating any specific fictional character.

---

# 23. Exposition breaks

Major lore moments deliberately stop the action.

The game freezes safely.

The interface reframes.

The player may:

- read,
- listen,
- skip,
- inspect associated objects.

These segments last roughly 20-90 seconds.

Then gameplay resumes immediately.

No exposition scene should punish a player for listening.

Typical content:

- founder recordings,
- weapon history,
- incident reports,
- bizarre HR policy,
- research explanations,
- FAITH/Frank conversations,
- employee correspondence.

This creates the rhythm:

**violence → quiet → absurd story → violence**

---

# 24. FAITH as narrator

FAITH replaces generic tutorial boxes wherever practical.

Instead of:

`PRESS R TO RELOAD`

FAITH explains the interaction in-character.

Frank may object when she explains obvious firearms handling to a retired Marine.

She becomes more useful when prototype equipment appears.

FAITH also handles:

- objectives,
- map annotations,
- facility status,
- security permissions,
- enemy classification,
- environmental warnings.

If a section temporarily disconnects FAITH, the absence should be immediately noticeable.

---

# 25. The 69 motif

The number appears frequently enough to become corporate folklore, but not in every numerical value.

Canonical examples:

- Directive `4761` = 69²
- Fazer Mk `LXIX`
- Level `69`
- security protocol `138`
- project `207`
- various historic corporate identifiers that are multiples of 69
- founder memorabilia in editions of 69

FAITH initially treats this as normal institutional history.

Frank eventually asks.

FAITH provides an obviously sanitized explanation.

Frank correctly rejects it.

---

# 26. Objectives

Objectives remain physically grounded.

Examples:

- restore emergency power,
- unlock containment stairwell,
- clear hostile signatures,
- isolate contaminated ventilation,
- reactivate fire doors,
- rescue trapped personnel,
- destroy escaped specimen,
- restore FAITH node,
- recover authorization token,
- restart coolant system,
- open service route.

“Kill everything” is often involved but not always the formal objective.

---

# 27. Secrets

Secrets reward curiosity rather than wall-humping every texture.

Hints include:

- floor plans,
- suspicious construction,
- inaccessible rooms visible through windows,
- maintenance labels,
- founder recordings,
- cables/pipes leading somewhere,
- FAITH being evasive.

Secret rewards include:

- weapons,
- rare ammunition,
- founder memorabilia,
- alternate routes,
- humorous documents,
- permanent small utility upgrades,
- prototype access.

No collectible exists solely to increase a percentage from 84.3% to 84.4%.

---

# 28. Progression

Frank does not gain RPG levels.

Progression comes from:

- arsenal expansion,
- access permissions,
- knowledge,
- shortcuts,
- recovered equipment,
- occasional physical equipment upgrades.

Possible upgrades:

- improved body armor,
- better flashlight,
- larger field medical kit,
- prototype targeting optic,
- better hearing protection that improves directional audio visualization,
- upgraded facility credentials.

Frank himself does not inexplicably gain +2% firearm damage after killing fifty zombies.

---

# 29. Health

Health is deliberately simple.

Frank has:

- health,
- body armor.

Medical supplies restore health.

Armor absorbs appropriate damage until degraded.

No regenerating health.

No hunger.

No thirst.

No crafting bandages from three napkins and a bottle of whiskey.

---

# 30. Death and checkpointing

Checkpoint on:

- floor transition,
- major objective completion,
- manual safe-room save where appropriate.

Death reloads quickly.

Target time from death to regained player control: under three seconds on normal hardware.

The game is violent, not punitive.

---

# 31. Difficulty

Difficulty should primarily affect:

- enemy aggressiveness,
- reaction time,
- damage,
- ammunition generosity,
- health supplies,
- encounter composition.

Never increase difficulty primarily by multiplying enemy hit points.

A Shambler should still behave like flesh when hit by a shotgun.

Harder difficulty means the situation becomes more dangerous, not that biology becomes ten times denser.

---

# 32. Audio

Guns need unusually strong sound design.

Priorities:

1. shot/transient,
2. mechanical action,
3. environment reflection,
4. impact,
5. casing/action sounds,
6. tail/reverb appropriate to room size.

A 1911 in an office should not sound like the same weapon in a concrete range tunnel.

FAITH remains clearly audible but can dynamically wait for combat intensity to fall before delivering nonessential dialogue.

---

# 33. Music

Music should support the old-school top-down brutality without drowning gun audio.

States:

- exploration,
- tension,
- combat,
- severe combat,
- boss,
- exposition silence/underscore.

Combat music may escalate based on active threat rather than scripted arena boundaries.

---

# 34. Boss encounters

Bosses are rare.

They are preferably consequences of the facility rather than arbitrary giant monsters.

Examples:

- containment organism integrated into manufacturing equipment,
- corrupted automated security platform,
- biological mass spread across several connected rooms,
- failed experimental subject whose environment must be manipulated,
- something from the excavation project that caused the entire problem.

Bosses use level geometry.

They should not simply be enormous health bars in empty circular rooms.

Although Frank may absolutely comment when he encounters an empty circular room.

---

# 35. Humor

The game never stops taking the physical danger seriously.

Comedy comes from:

- corporate normality,
- Frank's reactions,
- FAITH's operational interpretation,
- founder history,
- shooter conventions rationalized as company policy,
- absurd gun culture,
- institutional survival.

The undead are not constantly delivering jokes.

The joke is that the company's normal operation was already only slightly less ridiculous than the apocalypse.

---

# 36. Accessibility

Required:

- fully remappable controls,
- controller aim assistance settings,
- subtitle size/options,
- named speaker subtitles,
- color-independent access indicators,
- screen-shake slider including zero,
- flash reduction,
- gore controls,
- audio visualization for important directional sounds,
- hold/toggle settings for run and weapon wheel,
- difficulty change during campaign.

---

# 37. Technical architecture

The app owns the game.

Do not begin by creating `Novolis.GameEngine`.

Initial structure:

```text
FrankMoat/
  Game/
  Combat/
  Weapons/
  Actors/
  Levels/
  Narrative/
  Rendering/
  Audio/
  Input/
  Content/
```

Use Novolis libraries only where they already solve the problem cleanly.

Likely platform consumers:

```text
Novolis.Math
Novolis.Physics
Novolis.Rendering.Silk
Novolis.Storage
Novolis.Time
```

Anything game-specific remains game-specific until another actual application demonstrates reuse.

---

# 38. Simulation

Use a fixed gameplay timestep, nominally 60 Hz.

Rendering may run independently.

Gameplay world includes:

- actors,
- projectiles,
- doors,
- triggers,
- pickups,
- interactive machinery,
- damage,
- perception/noise,
- level state.

Cosmetic particles are not gameplay entities.

A room containing 20,000 blood droplets must not create 20,000 objects in the authoritative simulation.

---

# 39. Entity strategy

Do not introduce a general-purpose ECS merely because games traditionally attract ECS frameworks.

Use simple purpose-built collections first.

Dense component-like storage is appropriate for genuinely numerous things:

- projectiles,
- particles,
- decals,
- fragments.

Frank, enemies, doors and weapons may remain ordinary strongly typed domain objects until profiling proves otherwise.

Patterns serve performance and clarity, not fashion.

---

# 40. Rendering target

Target:

**60 FPS minimum at 1440p on ordinary desktop hardware.**

Stress scene:

- 150 active enemies,
- 500 gameplay projectiles,
- several thousand cosmetic particles,
- extensive historical blood,
- dozens of lights,
- persistent corpses,
- active environmental machinery.

Particle count may dynamically scale.

Gameplay entity count must not.

---

# 41. Testing

The renderer should never be required to validate gameplay.

Headless tests cover:

### Weapons

- magazine/tube/cylinder state,
- reload interruption,
- chamber logic,
- ammo conservation,
- penetration,
- rate of fire,
- overheating.

### Combat

- friendly fire,
- damage,
- death,
- armor,
- explosions,
- noise propagation.

### Levels

- entrance-to-exit reachability,
- mandatory access item availability,
- doors cannot permanently deadlock progression,
- secrets remain optional.

### Campaign

- Level 69 reachable from opening sequence,
- sequential upward route exists,
- Directive 4761 state transitions correctly,
- Level 0 final return is possible.

### Save/load

A save reproduces all gameplay-relevant state.

Cosmetic particle state does not need perfect persistence.

---

# 42. First vertical slice

Do not build 69 floors first.

The first playable slice contains:

## Level 0

Normal morning.

Lockdown.

Frank/FAITH introduction.

Founder elevator.

## Level 69

Safe founder shrine.

Weapon collection.

Range.

Founder recording.

Initial loadout.

## Level 68

First full combat floor.

Approximately 15 minutes.

Contains:

- Shambler,
- Runner,
- Contaminated Security,
- one environmental hazard,
- one loop/shortcut,
- one secret,
- one access gate,
- one large combat incident.

## Weapons

- M1911
- pump shotgun
- Winchester lever rifle
- one automatic firearm
- one explosive weapon

Fazer Mk LXIX exists physically on Level 69 but is inaccessible.

## Required technical features

- mouse/controller movement and aiming,
- firing,
- reload states,
- noise,
- enemy perception,
- blood particles,
- persistent blood,
- corpses,
- map,
- door/access system,
- FAITH dialogue,
- founder exposition screen,
- save/load.

The vertical slice is successful when playing Level 68 with the shotgun is fun even with every narrative element disabled.

---

# 43. Second milestone

Add:

- Levels 67-64,
- Engineering culture,
- first gun range outside Level 69,
- proper ammo economy,
- Engineering rotary weapon,
- first boss,
- more advanced gore flattening,
- weapon inspection,
- one temporary FAITH outage.

At this point the game should prove both its mechanical and narrative identity.

---

# 44. The ending

Frank eventually reaches Level 1.

Directive 4761 is nearly satisfied.

He enters Level 0.

The player walks through the same lobby used for the tutorial.

The geography is recognizable.

The post is still there.

Morning coffee is not.

FAITH completes her final containment checks.

> **FAITH:** “Directive 4761 remediation criteria satisfied.”

> **Frank:** “Doors.”

External locks disengage.

Daylight.

Frank walks toward the exit.

> **FAITH:** “Frank.”

He stops.

> **Frank:** “What?”

Long enough pause to cause genuine concern.

Then either:

### Clean ending

> **FAITH:** “Your shift ended fourteen hours ago.”

> **Frank:** “Good night, FAITH.”

or, for sequel bait:

> **FAITH:** “I am detecting one unresolved hostile signature.”

> **Frank:** “Where?”

> **FAITH:** “Level 69.”

Frank slowly turns around.

Cut to title.

---

# 45. What the game must never become

Not a roguelike.

Not a looter shooter.

Not a cover shooter.

Not a bullet-hell game.

Not an RPG with percentage-driven gun upgrades.

Not a survival-crafting game.

Not an arena shooter connected by corridors.

Not a generic retro-boomer-shooter skin.

Not a general game-engine project wearing a game as a hat.

The game is:

> **A fast, close-camera, top-down 2D shooter about a retired Marine gun nerd fighting his way upward through 69 levels of a catastrophically compromised research facility, accompanied by its facility AI, using an absurd collection of lovingly represented real and fictional firearms while the building progressively turns into modern art made from blood.**

And Frank would really, really like to go home.

Yes. **Very practical**, and I think it is the right choice.

You can keep the game **entirely 2D for simulation and collision** while rendering walls with a fixed apparent height. That gives you the *Loaded*-style “2.5D” look without turning Frank into a 3D game.

## The basic trick

Your floor geometry remains:

```text
top-down world

┌──────────────┐
│              │
│    room      │
│              │
└──────────────┘
```

Each wall edge has a `Height`.

At render time, project that height in a constant screen-space direction:

```text
base edge
A ───────── B

top edge
  A' ───────── B'
   ╲           ╲
    A ───────── B
```

So one wall becomes:

- base edge
- top edge offset by projected height
- vertical face connecting them
- optional top face

Something conceptually like:

```csharp
public readonly record struct WallSegment(
    Vector2 Start,
    Vector2 End,
    float Height);
```

Then:

```csharp
var extrusion = camera.WallHeightDirection * wall.Height;

var topStart = wall.Start + extrusion;
var topEnd   = wall.End + extrusion;
```

Strictly speaking those positions would be transformed through the world-to-screen projection, but that's the idea.

## The important part: gameplay remains 2D

Frank's collision system only knows:

```text
wall footprint
door footprint
enemy circles
projectile trajectories
```

It doesn't care that a wall visually extends upward.

That is excellent for us because your scary problem, **continuous collision detection**, remains:

```text
2D swept circle / segment / polygon
```

not:

```text
3D capsule versus extruded mesh
```

Huge complexity reduction.

---

# I'd actually make walls polygons, not sprites

Something like:

```csharp
public sealed record WallGeometry(
    Polygon2 Footprint,
    float Height,
    WallMaterial Material);
```

Rendering takes that footprint and derives:

- floor-facing boundary
- visible vertical faces
- top face

Physics just receives:

```csharp
Footprint
```

Now the wall can be:

```text
straight
L-shaped
diagonal
curved-ish through segments
irregular
```

without requiring bespoke assets.

## With a fixed camera this gets wonderfully cheap

Frank's camera doesn't rotate.

That means the visible wall faces are predictable.

Suppose your apparent height vector is:

```text
↖
```

Then only walls facing roughly south/east need their vertical faces rendered prominently.

You don't need general 3D hidden-surface machinery.

A fixed projection makes the entire problem almost suspiciously civilized.

---

# Occlusion is the bit we need to design carefully

This is the only part I would prototype before committing.

Frank must sometimes appear **behind** a raised wall:

```text
        top
   ┌───────────┐
   │           │
   │   Frank   │   <- visually hidden
   └───────────┘
```

and sometimes in front.

A plain `Layer = Wall` / `Layer = Actor` system won't be enough.

We need depth sorting based on world position.

Probably something like:

```csharp
SortKey = worldPosition.Y;
```

with separate components:

```text
floor
wall top/back
actors
wall front faces
foreground objects
effects
HUD
```

A wall can therefore render in more than one pass.

For example:

```text
1. floor
2. wall top/back portions
3. Frank/enemies/projectiles
4. foreground wall faces
5. particles above actors
6. HUD
```

That gives the illusion that Frank actually moves through spaces surrounded by raised geometry.

---

# Even better: fade walls that obscure Frank

Old top-down/isometric games often suffer from:

> “I am behind this very pretty wall and cannot see anything.”

We shouldn't.

If Frank is occluded, the relevant wall can:

- fade to 25–40%
- render only its outline
- cut away around Frank
- lower visually

I'd choose **local fade**, not entire-wall disappearance.

So:

```text
normal wall
████████████

Frank behind it
████░░░░████
    Frank
```

It preserves the architecture while maintaining readability.

This can be computed entirely from screen overlap, not physics.

---

# Doors become especially good

A raised wall system makes doors much more satisfying.

Instead of a line disappearing:

```text
closed:

████████████
████ door ██
████████████
```

we can have an actual visual wall opening:

```text
█████     █████
█████     █████
```

With:

- sliding blast doors
- shutters
- glass partitions
- security gates
- elevator doors

All still backed by simple 2D collision footprints.

And since this is Frank's world, a malfunctioning security shutter can absolutely crush an undead thing.

---

# Bullets remain on the floor plane

For normal shooting, the projectile physics remains entirely 2D.

The visible tracer can be rendered at a fixed apparent elevation:

```text
bullet simulation:
Vector2

render:
Vector2 + GunHeightOffset
```

That gives a small visual lift without adding vertical ballistics.

Likewise:

- muzzle flashes
- casings
- gore
- sparks

can have fake visual Z.

This distinction is useful:

```text
X/Y = gameplay world position
Z   = presentation elevation
```

You can expose something like:

```csharp
public readonly record struct SpritePose2D(
    Vector2 Position,
    float Elevation,
    float Rotation);
```

`Elevation` affects rendering only.

Frank can therefore throw a casing visibly upward and let it fall while its gameplay impact is zero.

---

# Gore benefits enormously

This style gives blood more surfaces to hit.

A shotgun blast near a wall can create:

```text
floor decal
wall decal
spray particles
chunks
```

because the renderer knows the wall has a face.

Even though gameplay collision says only:

```text
bullet hit wall segment at P
normal = N
```

the presentation system can map that impact onto the visible wall face and paint a blood/scorch decal there.

That will make the rooms look substantially richer than pure flat top-down rendering.

---

# Lights also become visually stronger

You don't need full 3D lighting.

You can fake:

- wall-face brightness
- top-face brightness
- muzzle flash lighting
- directional shadows

using wall normals and a simple fixed light model.

For example:

```text
top face          1.00
lit wall face     0.85
shadow wall face  0.55
```

Then dynamic muzzle flashes can briefly add to nearby surfaces.

Even a very dumb lighting model will look convincing because the geometry establishes depth.

---

# I'd formalize it as 2D + elevation, not 2.5D physics

Something like:

```text
Novolis.Rendering.TwoD
    Sprite
    Polygon
    Camera

Novolis.Rendering.TwoD.Height
    ExtrudedPolygon
    RaisedWall
    RaisedDoor
    ElevatedSprite
    Occlusion
```

Though I would **not extract this immediately**.

Prototype it inside the lab first: `d:\novolis\novolis-lab\labs\gaming\FrankMoat` (single-room Loaded-style minigun range).

If it proves generically useful, then extract the primitive.

## Perfect extension to the minigun range

Actually, the `MinigunRange` is an ideal place to prove it.

Make the 50×50 m range:

```text
┌──────────────────────────────┐
│                              │
│   angled steel wall          │
│       ╱                      │
│      ╱                       │
│                             │
│          ⭕ player zone      │
│                             │
│                   target    │
│                              │
└──────────────────────────────┘
```

But render those walls with maybe **2.5–3 m apparent height**.

Then we validate simultaneously:

- Silk 2D geometry
- raised wall rendering
- depth sorting
- occlusion
- projectile collision
- ricochet normals
- wall impact effects

while physics still sees a beautifully boring flat rectangle.

That is exactly the sort of separation I'd want:

> **The renderer lies convincingly.  
> The physics remains boring and correct.**

For Frank, that's probably the sweet spot.