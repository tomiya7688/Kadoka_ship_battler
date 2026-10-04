using KadokaShipBattler.Core;
using KadokaShipBattler.Characters;
using System.Text.Json;
using KadokaShipBattler.Navigation;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
    Console.WriteLine($"PASS: {description}");
}
var player = new ShipBattleState(TeamSide.Player, 100, 30);
var enemy = new ShipBattleState(TeamSide.Enemy, 100, 30);
var carry = new AmmoCarryState();
var cannon = new CannonState();
var ammo = new TestAmmo(25);
Check(!carry.TryPickup(ammo, false), "Capability prevents pickup");
Check(!carry.TryPickup(null, true), "Null ammo rejected");
Check(!cannon.FireAt(player, enemy), "Empty cannon cannot fire");
Check(!enemy.TryDamageCore(25), "Intact hull protects core");
foreach (var bad in new[] { float.NaN, float.PositiveInfinity, -1f })
{
    Check(!carry.TryPickup(new TestAmmo(bad), true), "Invalid ammo rejected: " + bad);
    Check(!enemy.ApplyHullDamage(bad), "Invalid hull damage rejected: " + bad);
}
for (var shot = 0; shot < 4; shot++)
{
    Check(carry.TryPickup(ammo, true), "Pickup round " + shot);
    Check(!carry.TryPickup(ammo, true), "No duplicate carry");
    Check(!cannon.TryLoadFrom(carry, player, TeamSide.Enemy) && ReferenceEquals(carry.CarriedAmmo, ammo), "Enemy cannot load player cannon");
    Check(cannon.TryLoadFrom(carry, player, TeamSide.Player) && carry.CarriedAmmo == null, "Load transfers ammo");
    Check(!cannon.FireAt(player, player) && ReferenceEquals(cannon.LoadedAmmo, ammo), "Friendly fire preserves ammo");
    Check(cannon.FireAt(player, enemy) && cannon.LoadedAmmo == null, "Shot consumes ammo");
    Check(enemy.CurrentHull == 100 - (shot + 1) * 25, "Hull damage correct");
}
Check(enemy.IsHullBreached && !enemy.IsDestroyed && enemy.CurrentCore == 30, "Hull zero does not end battle");
Check(carry.TryPickup(ammo, true) && cannon.TryLoadFrom(carry, player, TeamSide.Player) && cannon.FireAt(player, enemy), "Combat continues after hull zero");
Check(enemy.TryDamageCore(25) && !enemy.IsDestroyed, "Exposed core takes damage");
Check(enemy.TryDamageCore(25) && enemy.IsDestroyed && enemy.CurrentCore == 0, "Core destruction ends battle and clamps health");
Check(!enemy.TryDamageCore(25), "Destroyed core cannot be attacked again");
Check(carry.TryPickup(ammo, true) && cannon.TryLoadFrom(carry, player, TeamSide.Player), "Load final round");
Check(!cannon.FireAt(player, enemy) && ReferenceEquals(cannon.LoadedAmmo, ammo), "Destroyed target preserves ammo");
var breachedOwner = new ShipBattleState(TeamSide.Player, 10, 30);
breachedOwner.ApplyHullDamage(10);
Check(cannon.FireAt(breachedOwner, new ShipBattleState(TeamSide.Enemy, 100, 30)), "Breached owner can still fire");
var control = new CrewControlState();
var leader = new TestCrew(TeamSide.Player);
var ally = new TestCrew(TeamSide.Player);
Check(!control.Register(null) && !control.CycleNext(), "Empty control roster is safe");
Check(!control.Register(new TestCrew(TeamSide.Enemy)), "Enemy cannot enter player control roster");
Check(control.Register(leader) && leader.DirectControl && ReferenceEquals(control.Current, leader), "First ally receives direct control");
Check(!control.Register(leader), "Duplicate crew rejected");
Check(control.Register(ally) && !ally.DirectControl && leader.DirectControl, "Second ally starts under AI");
Check(!control.TrySwitch(new TestCrew(TeamSide.Player)) && !control.TrySwitch(null), "Unregistered and null targets rejected");
Check(!control.TrySwitch(leader), "Same-target switch is a no-op");
Check(control.CycleNext() && ally.DirectControl && !leader.DirectControl, "Switch transfers control to exactly one crew");
Check(leader.AiResumes == 1, "Released leader immediately resumes AI");
Check(control.CycleNext() && leader.DirectControl && !ally.DirectControl, "Cycling wraps to leader");
ally.IsAvailable = false;
Check(!control.TrySwitch(ally) && !control.CycleNext(), "Unavailable ally cannot receive control");
leader.IsAvailable = false;
control.EnsureAvailableSelection();
Check(control.Current == null && !leader.DirectControl, "No available crew releases control safely");
ally.IsAvailable = true;
control.EnsureAvailableSelection();
Check(ReferenceEquals(control.Current, ally) && ally.DirectControl, "Available ally recovers selection");
control.ReleaseAll();
Check(control.Current == null && control.Members.Count == 0 && !ally.DirectControl, "Teardown clears roster and control");
foreach (var example in new[] { (2f, 2f, true), (2f, 3f, true), (3f, 3f, false) })
{
    var bag = new AmmoCarryState(5, 2);
    var firstAmmo = new TestAmmo(25, example.Item1);
    var secondAmmo = new TestAmmo(25, example.Item2);
    Check(bag.CanPickup(firstAmmo, true) && bag.Count == 0, "Pickup preview does not mutate inventory");
    Check(bag.TryPickup(firstAmmo, true), "First weighted pickup " + example.Item1);
    Check(bag.CanPickup(secondAmmo, true) == example.Item3, "Combined weight preview " + example);
    Check(bag.TryPickup(secondAmmo, true) == example.Item3, "Combined weight enforcement " + example);
    Check(bag.Count == (example.Item3 ? 2 : 1) && bag.CurrentWeight == example.Item1 + (example.Item3 ? example.Item2 : 0), "Rejected pickup preserves contents and weight");
}
var heavyBag = new AmmoCarryState(5, 2);
Check(heavyBag.TryPickup(new TestAmmo(25, 5), true), "Single item at exact weight capacity accepted");
Check(!heavyBag.TryPickup(new TestAmmo(25, 1), true) && heavyBag.Count == 1, "Weight limit blocks addition even with count space");
var countBag = new AmmoCarryState(100, 2);
Check(countBag.TryPickup(new TestAmmo(25, 1), true) && countBag.TryPickup(new TestAmmo(25, 1), true), "Two items below weight limit accepted");
Check(!countBag.TryPickup(new TestAmmo(25, 0), true) && countBag.CurrentWeight == 2, "Count limit blocks even zero-weight item");
Check(!new AmmoCarryState(5, 0).TryPickup(ammo, true), "Zero count limit disables carrying");
var zeroBag = new AmmoCarryState(0, 1);
Check(!zeroBag.TryPickup(ammo, true) && zeroBag.TryPickup(new TestAmmo(25, 0), true), "Zero capacity accepts only zero weight");
foreach (var invalidWeight in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
{
    var bag = new AmmoCarryState(5, 2);
    Check(!bag.CanPickup(new TestAmmo(25, invalidWeight), true), "Invalid weight preview rejected: " + invalidWeight);
    Check(!bag.TryPickup(new TestAmmo(25, invalidWeight), true) && bag.Count == 0 && bag.CurrentWeight == 0, "Invalid weight cannot change inventory: " + invalidWeight);
    var rejected = false;
    try { bag.ConfigureLimits(invalidWeight, 2); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected && bag.CarryCapacity == 5, "Invalid capacity cannot change limits: " + invalidWeight);
}
var invalidCountRejected = false;
try { new AmmoCarryState(5, -1); } catch (ArgumentOutOfRangeException) { invalidCountRejected = true; }
Check(invalidCountRejected, "Negative count limit rejected");
var queuedBag = new AmmoCarryState(5, 2);
var light = new TestAmmo(25, 2);
var heavy = new TestAmmo(40, 3);
Check(queuedBag.TryPickup(light, true) && queuedBag.TryPickup(heavy, true), "Queue two different rounds");
Check(queuedBag.Items.Count == 2 && ReferenceEquals(queuedBag.CarriedAmmo, light), "First pickup remains first to load");
var queuedCannon = new CannonState();
var queuedTarget = new ShipBattleState(TeamSide.Enemy, 100, 30);
Check(queuedCannon.TryLoadFrom(queuedBag, player, TeamSide.Player) && queuedBag.Count == 1 && queuedBag.CurrentWeight == 3, "Load consumes only the first item and its weight");
Check(!queuedCannon.TryLoadFrom(queuedBag, player, TeamSide.Player) && queuedBag.Count == 1, "Loaded cannon preserves remaining inventory");
Check(queuedCannon.FireAt(player, queuedTarget) && queuedTarget.CurrentHull == 75, "First queued round damage correct");
Check(queuedCannon.TryLoadFrom(queuedBag, player, TeamSide.Player) && queuedBag.Count == 0 && queuedBag.CurrentWeight == 0, "Final load clears weight exactly");
Check(queuedCannon.FireAt(player, queuedTarget) && queuedTarget.CurrentHull == 35, "Second queued round damage correct");
Check(queuedBag.TakeAmmo() == null && queuedBag.CurrentWeight == 0, "Empty take preserves zero weight");
var reducedBag = new AmmoCarryState(5, 2);
reducedBag.TryPickup(light, true);
reducedBag.TryPickup(heavy, true);
reducedBag.ConfigureLimits(1, 1);
Check(reducedBag.Count == 2 && reducedBag.CurrentWeight == 5 && !reducedBag.CanPickup(ammo, true), "Reduced limits preserve held items and prevent additions");
Check(ReferenceEquals(reducedBag.TakeAmmo(), light) && ReferenceEquals(reducedBag.TakeAmmo(), heavy) && reducedBag.CurrentWeight == 0, "Over-limit inventory can still be unloaded safely");
var crewJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PrototypeCrew.json"));
var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
BattleCrewDefinition ReadCrew() => JsonSerializer.Deserialize<BattleCrewDefinition>(crewJson, jsonOptions);
var crewSetup = ReadCrew();
crewSetup.Validate();
Check(crewSetup.GetTeam(TeamSide.Player).Length == 5 && crewSetup.GetTeam(TeamSide.Enemy).Length == 5, "Production JSON defines five crew per team");
Check(crewSetup.GetTeam(TeamSide.Player)[0].id == "leader", "First formation slot is leader");
Check(crewSetup.GetTeam(TeamSide.Player)[1].attackMode == NormalAttackMode.Ranged, "Gunner defines a ranged attack");
Check(crewSetup.GetTeam(TeamSide.Player)[2].maxCarryCount == 3 && crewSetup.GetTeam(TeamSide.Player)[2].carryCapacity == 9, "Carrier defines its own carrying stats");
Check(crewSetup.GetTeam(TeamSide.Player)[3].canFly && crewSetup.GetTeam(TeamSide.Player)[3].canPhase, "Scout defines flight and phase flags");
Check(crewSetup.GetTeam(TeamSide.Player)[4].maxHp == 180 && crewSetup.GetTeam(TeamSide.Player)[4].projectileHardness == 5, "Defender defines HP and projectile hardness");
void InvalidCrew(Action action, string description)
{
    var rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; }
    Check(rejected, description);
}
foreach (var size in new[] { 0, 4, 6 })
{
    var invalid = ReadCrew();
    invalid.player.slots = Enumerable.Repeat("leader", size).ToArray();
    InvalidCrew(invalid.Validate, "Reject player formation size " + size);
    invalid = ReadCrew();
    invalid.enemy.slots = Enumerable.Repeat("leader", size).ToArray();
    InvalidCrew(invalid.Validate, "Reject enemy formation size " + size);
}
var unknownCrew = ReadCrew();
unknownCrew.player.slots[2] = "missing";
InvalidCrew(unknownCrew.Validate, "Reject unknown formation character");
var duplicateCrew = ReadCrew();
duplicateCrew.characters[1].id = duplicateCrew.characters[0].id;
InvalidCrew(duplicateCrew.Validate, "Reject duplicate character definitions");
var repairCrew = ReadCrew();
repairCrew.characters[0].capabilities |= CharacterCapability.Repair;
InvalidCrew(repairCrew.Validate, "Repair cannot be assigned to characters");
foreach (var badHp in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
{
    var invalid = ReadCrew();
    invalid.characters[0].maxHp = badHp;
    InvalidCrew(invalid.Validate, "Reject invalid character HP " + badHp);
}
var invalidAttack = ReadCrew();
invalidAttack.characters[0].attackMode = (NormalAttackMode)99;
InvalidCrew(invalidAttack.Validate, "Reject unknown attack mode");
var invalidRange = ReadCrew();
invalidRange.characters[0].attackRange = 0;
InvalidCrew(invalidRange.Validate, "Attack needs a positive range");
var health = new CrewHealthState(100);
Check(health.ApplyDamage(25) && health.CurrentHp == 75 && health.IsAlive, "Crew HP takes normal damage");
foreach (var badDamage in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
    Check(!health.ApplyDamage(badDamage) && health.CurrentHp == 75, "Reject invalid crew damage " + badDamage);
Check(health.ApplyDamage(1000) && !health.IsAlive && health.CurrentHp == 0, "Lethal damage clamps crew HP to zero");
Check(!health.ApplyDamage(10), "Dead crew cannot take further damage");
var replacement = new CharacterStats { id = "new-ally", displayName = "New Ally", maxHp = 80, moveSpeed = 3 };
crewSetup.characters = crewSetup.characters.Append(replacement).ToArray();
crewSetup.player.slots[4] = "new-ally";
Check(crewSetup.GetTeam(TeamSide.Player)[4].id == "new-ally", "Additional character data can replace a formation slot");
var ammoJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PrototypeAmmo.json"));
KadokaShipBattler.Ammo.BattleAmmoDefinition ReadAmmo() => JsonSerializer.Deserialize<KadokaShipBattler.Ammo.BattleAmmoDefinition>(ammoJson, jsonOptions);
var ammoSetup = ReadAmmo();
ammoSetup.Validate();
Check(ammoSetup.player.slots.Length == 25 && ammoSetup.enemy.slots.Length == 25, "Production ammo JSON defines 25 slots per team");
Check(ammoSetup.player.slots.Count(id => id == "cannonball") == 15 && ammoSetup.player.slots.Count(id => id == "heavy-cannonball") == 10, "Repeated ammo slots preserve actual type counts");
Check(ammoSetup.ammo[0].hardness == 2 && ammoSetup.ammo[1].hardness == 4, "Ammo definitions include hardness");
Check(ammoSetup.GetLayout(TeamSide.Player).id != ammoSetup.GetLayout(TeamSide.Enemy).id, "Ship types select separate spawn layouts");
foreach (var size in new[] { 0, 24, 26 })
{
    var invalid = ReadAmmo();
    invalid.player.slots = Enumerable.Repeat("cannonball", size).ToArray();
    InvalidCrew(invalid.Validate, "Reject player ammo deck size " + size);
    invalid = ReadAmmo();
    invalid.enemy.slots = Enumerable.Repeat("cannonball", size).ToArray();
    InvalidCrew(invalid.Validate, "Reject enemy ammo deck size " + size);
}
var missingAmmo = ReadAmmo();
missingAmmo.player.slots[0] = "missing";
InvalidCrew(missingAmmo.Validate, "Reject unknown ammo id");
var duplicateAmmo = ReadAmmo();
duplicateAmmo.ammo[1].id = duplicateAmmo.ammo[0].id;
InvalidCrew(duplicateAmmo.Validate, "Reject duplicate ammo definition ids");
foreach (var bad in new[] { -1f, float.NaN, float.PositiveInfinity })
{
    var invalid = ReadAmmo();
    invalid.ammo[0].weight = bad;
    InvalidCrew(invalid.Validate, "Reject invalid ammo weight " + bad);
    invalid = ReadAmmo();
    invalid.ammo[0].hardness = bad;
    InvalidCrew(invalid.Validate, "Reject invalid ammo hardness " + bad);
    invalid = ReadAmmo();
    invalid.ammo[0].damage = bad;
    InvalidCrew(invalid.Validate, "Reject invalid ammo damage " + bad);
}
var badLayout = ReadAmmo();
badLayout.enemyLayout = "missing";
InvalidCrew(badLayout.Validate, "Reject unknown ship spawn layout");
badLayout = ReadAmmo();
badLayout.layouts[0].points[0].x = float.NaN;
InvalidCrew(badLayout.Validate, "Reject invalid spawn coordinates");
badLayout = ReadAmmo();
badLayout.layouts[0].points[1] = badLayout.layouts[0].points[0];
InvalidCrew(badLayout.Validate, "Reject duplicate spawn points");
var ammoLookup = ammoSetup.ammo.ToDictionary(item => item.id);
var deckEntries = ammoSetup.player.slots.Select(id => (IAmmo)ammoLookup[id]).ToArray();
var deck = new KadokaShipBattler.Ammo.AmmoDeckState(deckEntries, 42);
var sameSeedDeck = new KadokaShipBattler.Ammo.AmmoDeckState(deckEntries, 42);
var liveRounds = Enumerable.Range(0, 25).Select(_ => deck.TrySpawn()).ToArray();
var replayRounds = Enumerable.Range(0, 25).Select(_ => sameSeedDeck.TrySpawn()).ToArray();
Check(liveRounds.Select(item => item.Slot).Distinct().Count() == 25 && deck.ActiveCount == 25, "All 25 physical deck slots can be active exactly once");
Check(deck.WaitingCount == 0 && deck.TrySpawn() == null, "Exhausted deck cannot exceed 25 live rounds");
Check(liveRounds.Count(item => ReferenceEquals(item.Definition, ammoLookup["cannonball"])) == 15, "Live light ammo count never exceeds its 15 slots");
Check(liveRounds.Count(item => ReferenceEquals(item.Definition, ammoLookup["heavy-cannonball"])) == 10, "Live heavy ammo count never exceeds its 10 slots");
Check(liveRounds.Select(item => item.Slot).SequenceEqual(replayRounds.Select(item => item.Slot)), "Seeded random deck can replay for verification");
Check(!liveRounds.Select(item => item.Slot).SequenceEqual(Enumerable.Range(0, 25)), "Ammo spawn order is shuffled among waiting slots");
var deckBag = new AmmoCarryState(100, 25);
var managed = liveRounds[0];
Check(deckBag.CanPickup(managed, true) && managed.Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Ground, "Pickup preview does not change deck state");
Check(deckBag.TryPickup(managed, true) && managed.Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Carried, "Managed pickup transfers ground round to inventory");
Check(!deckBag.TryPickup(managed, true) && !new AmmoCarryState(100, 25).TryPickup(managed, true), "Same round cannot be carried twice or by two owners");
Check(deck.ActiveCount == 25 && deck.TrySpawn() == null, "Carried ammo still occupies deck slot");
var managedCannon = new CannonState();
var deckOwner = new ShipBattleState(TeamSide.Player, 100, 30);
var deckTarget = new ShipBattleState(TeamSide.Enemy, 100, 30);
Check(!managedCannon.TryLoadFrom(deckBag, deckOwner, TeamSide.Enemy) && managed.Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Carried, "Rejected load preserves managed round ownership");
Check(managedCannon.TryLoadFrom(deckBag, deckOwner, TeamSide.Player) && managed.Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Loaded && deckBag.Count == 0, "Loading transfers same round to cannon");
Check(!managedCannon.FireAt(deckOwner, deckOwner) && managed.Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Loaded, "Rejected shot retains loaded round");
Check(deck.TrySpawn() == null, "Loaded ammo still occupies deck slot");
Check(managedCannon.FireAt(deckOwner, deckTarget) && managed.Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Returned && deck.WaitingCount == 1, "Impact returns fired ammo to waiting pool");
var nextAppearance = deck.TrySpawn();
Check(nextAppearance.Slot == managed.Slot && !ReferenceEquals(nextAppearance, managed), "Returned slot creates a fresh appearance handle");
Check(!managed.ReturnToDeck() && !managed.TryCarry() && !managed.TryLoad() && deck.ActiveCount == 25, "Stale appearance cannot release or reuse a newly spawned round");
Check(liveRounds[1].ReturnToDeck() && deck.WaitingCount == 1, "Ground despawn returns deck slot");
Check(!liveRounds[1].ReturnToDeck() && deck.WaitingCount == 1, "Double return cannot inflate waiting count");
deckBag.TryPickup(liveRounds[2], true);
deckBag.TryPickup(liveRounds[3], true);
deckBag.Clear();
Check(deckBag.Count == 0 && deckBag.CurrentWeight == 0 && liveRounds[2].Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Returned && liveRounds[3].Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Returned, "Inventory cleanup returns every carried round and clears weight");
deckBag.TryPickup(liveRounds[4], true);
managedCannon.TryLoadFrom(deckBag, deckOwner, TeamSide.Player);
managedCannon.Clear();
Check(managedCannon.LoadedAmmo == null && liveRounds[4].Stage == KadokaShipBattler.Ammo.AmmoRoundStage.Returned, "Cannon cleanup returns loaded round");
deck.Close();
Check(deck.ActiveCount == 0 && deck.WaitingCount == 25 && deck.TrySpawn() == null, "Closing scene returns all rounds and prevents later spawning");
deck.Close();
Check(deck.WaitingCount == 25, "Repeated deck cleanup is safe");
InvalidCrew(() => new KadokaShipBattler.Ammo.AmmoDeckState(new IAmmo[24], 0), "Runtime deck rejects wrong slot count");
InvalidCrew(() => new KadokaShipBattler.Ammo.AmmoDeckState(new IAmmo[25], 0), "Runtime deck rejects null slot definitions");
var cone = new KadokaShipBattler.AI.VisionCone(120, 6);
Check(cone.Contains(0, 0, 0, -1, 0, -1), "Vision sees targets in front");
Check(!cone.Contains(0, 0, 0, -1, 0, 1), "Vision excludes targets behind");
Check(!cone.Contains(0, 0, 0, -1, 1, 0), "Vision excludes targets beside a 120-degree cone");
Check(cone.Contains(0, 0, 0, -1, 1.7320508f, -1), "Vision includes cone boundary");
Check(!cone.Contains(0, 0, 0, -1, 1.74f, -1), "Vision excludes targets just outside cone");
Check(cone.Contains(0, 0, 0, -1, 0, -6), "Vision includes exact distance boundary");
Check(!cone.Contains(0, 0, 0, -1, 0, -6.01f), "Vision excludes targets beyond distance");
Check(cone.Contains(0, 0, 0, -1, 0, 0), "Target at the observer position is visible");
Check(cone.Contains(10, 20, 0, -1, 10, 19), "Vision follows translated observer position");
Check(cone.Contains(0, 0, 0, -4, 0, -1), "Vision normalizes facing direction");
Check(!cone.Contains(0, 0, 0, 0, 0, -1), "Zero facing does not imply omnidirectional vision");
Check(cone.Contains(0, 0, 1, 0, 1, 0) && !cone.Contains(0, 0, 1, 0, -1, 0), "Turning updates which side is visible");
Check(new KadokaShipBattler.AI.VisionCone(360, 6).Contains(0, 0, 0, -1, 0, 1), "360-degree sensor sees behind within distance");
Check(!new KadokaShipBattler.AI.VisionCone(360, 1).Contains(0, 0, 1, 0, 0, -2), "Full-angle sensor still enforces distance");
foreach (var angle in new[] { 0f, -1f, 361f, float.NaN, float.PositiveInfinity })
    InvalidCrew(() => new KadokaShipBattler.AI.VisionCone(angle, 6), "Reject invalid vision angle " + angle);
foreach (var distance in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
    InvalidCrew(() => new KadokaShipBattler.AI.VisionCone(120, distance), "Reject invalid vision distance " + distance);
foreach (var coordinate in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Check(!cone.Contains(coordinate, 0, 0, -1, 0, -1), "Reject nonfinite observer coordinates " + coordinate);
    Check(!cone.Contains(0, 0, coordinate, -1, 0, -1), "Reject nonfinite facing " + coordinate);
    Check(!cone.Contains(0, 0, 0, -1, coordinate, -1), "Reject nonfinite target coordinates " + coordinate);
}
var memory = new KadokaShipBattler.AI.VisionMemory();
var seen = new KadokaShipBattler.AI.VisionObservation(11, KadokaShipBattler.AI.ObservedTargetKind.Crew, TeamSide.Enemy, 0, -1, 80, 0, 0, 1);
var seenAmmo = new KadokaShipBattler.AI.VisionObservation(12, KadokaShipBattler.AI.ObservedTargetKind.Ammo, TeamSide.Player, 0, -2, 0, 2, 25, 1);
memory.Replace(new[] { seenAmmo, seen, seen });
Check(memory.Observations.Count == 2 && memory.IsCurrent(seen) && memory.IsCurrent(seenAmmo), "Vision memory deduplicates currently observed targets");
Check(memory.Observations[0].TargetId == 11 && memory.Observations[1].TargetId == 12, "Vision memory has deterministic target order");
var oldList = memory.Observations;
var updatedSeen = new KadokaShipBattler.AI.VisionObservation(11, KadokaShipBattler.AI.ObservedTargetKind.Crew, TeamSide.Enemy, 0, -0.5f, 70, 0, 0, 2);
memory.Replace(new[] { updatedSeen });
Check(memory.Observations.Count == 1 && memory.IsCurrent(updatedSeen) && !memory.IsCurrent(seen) && !memory.IsCurrent(seenAmmo), "New scan removes absent targets and invalidates previous snapshots");
Check(seen.Hp == 80 && seen.Y == -1 && oldList.Count == 2, "Old observations stay immutable rather than reading live hidden state");
memory.Remove(11);
Check(memory.Observations.Count == 0 && !memory.IsCurrent(updatedSeen), "Blocked target can be immediately removed before next scan");
memory.Remove(999);
Check(memory.Observations.Count == 0, "Removing an unknown observation is safe");
memory.Replace(new[] { seen });
memory.Clear();
Check(memory.Observations.Count == 0 && !memory.IsCurrent(seen) && !memory.IsCurrent(null), "Disable or death clears all current observations");
InvalidCrew(() => memory.Replace(new KadokaShipBattler.AI.VisionObservation[] { null }), "Reject null observation entry");
var mapJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PrototypeNavigation.json"));
BattleMapData Maps() => JsonSerializer.Deserialize<BattleMapData>(mapJson, new JsonSerializerOptions { IncludeFields = true });
var maps = Maps(); maps.Validate();
var navigation = new BattleNavigation(maps, "standard", "cargo");
Check(navigation.Rooms.Count == 7 && navigation.Doors.Count == 10, "Two distinct three-room ships connected by one bridge");
var detachedBounds = navigation.Rooms[0].Bounds; detachedBounds.maxX = 100;
Check(navigation.Rooms[0].Bounds.maxX == -4, "Published room bounds cannot mutate navigation geometry");
maps.types[0].rooms[0].maxX = 100;
Check(navigation.Rooms[0].Bounds.maxX == -4, "Runtime map does not retain mutable input room data");
bool Route(NavPoint from, NavPoint goal, TraversalAbilities flags, bool breached, TeamSide side = TeamSide.Player) =>
    navigation.TryFindPath(from, goal, flags, side, breached, breached, out _);
bool Segment(NavPoint from, NavPoint goal, TraversalAbilities flags = TraversalAbilities.None, bool breached = false) =>
    navigation.CanTraverse(from, goal, flags, TeamSide.Player, breached, breached);
var roomStart = new NavPoint(-5, -1.5f); var cannonGoal = new NavPoint(-2, 0);
Check(Route(roomStart, cannonGoal, 0, false), "Walker reaches cannon through normal door");
Check(!Segment(roomStart, new NavPoint(-3, -1.5f)), "Closed phase door prevents direct walking and tunneling");
Check(Segment(roomStart, new NavPoint(-3, -1.5f), TraversalAbilities.Phase), "Phase crew crosses closed phase door");
Check(!Segment(roomStart, new NavPoint(-3, -1.5f), TraversalAbilities.Fly), "Flight does not bypass a closed phase door");
Check(!Segment(new NavPoint(-3, 1.7f), new NavPoint(-5, 1.7f)), "Walker cannot cross flight gap");
Check(Segment(new NavPoint(-3, 1.7f), new NavPoint(-5, 1.7f), TraversalAbilities.Fly), "Flyer crosses flight gap");
foreach (var flags in new[] { TraversalAbilities.None, TraversalAbilities.Fly, TraversalAbilities.Phase, TraversalAbilities.Fly | TraversalAbilities.Phase })
{
    Check(!Route(roomStart, new NavPoint(6, 1.5f), flags, false), "Intact hull blocks boarding for " + flags);
    Check(Route(roomStart, new NavPoint(6, 1.5f), flags, true), "Breach permits shared bridge route for " + flags);
    Check(Route(new NavPoint(6, 1.5f), cannonGoal, flags, true), "Boarded crew can return with " + flags);
}
Check(navigation.TryFindPath(roomStart, new NavPoint(6, 1.5f), 0, TeamSide.Player, false, true, out var boarding), "Player boarding checks enemy hull only");
Check(boarding.Any(p => Math.Abs(p.x + 1) < 0.04f) && boarding.Any(p => Math.Abs(p.x - 1) < 0.04f), "Boarding path contains both bridge entrances");
var cursorPoint = roomStart;
foreach (var step in boarding)
{
    Check(navigation.CanTraverse(cursorPoint, step, 0, TeamSide.Player, false, true), "Every computed boarding segment is traversable");
    cursorPoint = step;
}
Check(!navigation.TryFindPath(new NavPoint(2, 0), new NavPoint(-6, 1.5f), 0, TeamSide.Enemy, false, true, out _), "Enemy boarding checks player hull independently");
Check(!Route(new NavPoint(float.NaN, 0), cannonGoal, 0, true), "Nonfinite start rejected");
Check(!Route(roomStart, new NavPoint(0, 2), 0, true), "Off-deck target rejected");
Check(!Segment(new NavPoint(-2, 2), new NavPoint(2, 2), 0, true), "Segment cannot tunnel across empty water");
Check(navigation.SetDoorOpen("Player/main-door", false) && navigation.Revision == 1, "Closing door invalidates cached paths");
Check(!Route(roomStart, cannonGoal, 0, false), "Closed normal door disconnects walker from deck");
Check(Route(roomStart, cannonGoal, TraversalAbilities.Phase, false), "Phase shortcut remains a valid alternate path");
Check(Route(roomStart, cannonGoal, TraversalAbilities.Fly, false), "Flyer can route via core and flight gap");
Check(!navigation.SetDoorOpen("unknown", true) && !navigation.SetDoorOpen("Player/main-door", false), "Unknown and unchanged doors do not change revision");
Check(navigation.SetDoorOpen("Player/main-door", true) && Route(roomStart, cannonGoal, 0, false), "Reopening door restores walker route");
Check(navigation.TryFindPath(new NavPoint(-4.1f, 0), new NavPoint(-4.1f, 1.7f), TraversalAbilities.Fly, TeamSide.Player, false, false, out var sameEdgePath), "Flyer can route between doors on the same wall");
cursorPoint = new NavPoint(-4.1f, 0);
foreach (var step in sameEdgePath)
{
    Check(Segment(cursorPoint, step, TraversalAbilities.Fly), "Same-wall portal route stays inside room interiors");
    cursorPoint = step;
}
InvalidCrew(() => { var bad = Maps(); bad.types[0].rooms[0].minX = float.NaN; bad.Validate(); }, "Reject nonfinite room bounds");
InvalidCrew(() => { var bad = Maps(); bad.types[0].rooms[1].minX = -1; bad.Validate(); }, "Reject overlapping room interiors");
InvalidCrew(() => { var bad = Maps(); bad.types[0].doors[0].to = "missing"; bad.Validate(); }, "Reject missing connection room");
InvalidCrew(() => { var bad = Maps(); bad.types[0].doors[0].point.x = 1; bad.Validate(); }, "Reject door outside shared edge");
InvalidCrew(() => { var bad = Maps(); bad.types[0].doors[0].width = 20; bad.Validate(); }, "Reject door wider than shared edge");
InvalidCrew(() => { var bad = Maps(); bad.types[0].doors[0].requiredAbilities = (TraversalAbilities)4; bad.Validate(); }, "Reject unknown traversal flags");
InvalidCrew(() => new BattleNavigation(Maps(), "missing", "cargo"), "Reject undefined ship type");
Console.WriteLine($"{checks} gameplay checks passed.");
sealed record TestAmmo(float Damage, float Weight = 1) : IAmmo;
sealed class TestCrew(TeamSide side) : IControlledCrew
{
    public TeamSide TeamSide => side;
    public bool IsAvailable { get; set; } = true;
    public bool DirectControl { get; private set; }
    public int AiResumes { get; private set; }
    public void SetDirectControl(bool value)
    {
        if (DirectControl && !value) AiResumes++;
        DirectControl = value;
    }
}
