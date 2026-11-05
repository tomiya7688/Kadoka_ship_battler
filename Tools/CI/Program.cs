using KadokaShipBattler.Core;

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
