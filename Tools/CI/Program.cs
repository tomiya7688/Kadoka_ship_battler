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
Console.WriteLine($"{checks} gameplay checks passed.");
sealed record TestAmmo(float Damage) : IAmmo;
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
