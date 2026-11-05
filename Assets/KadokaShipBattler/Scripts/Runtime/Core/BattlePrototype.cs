using System.Collections.Generic;
using KadokaShipBattler.AI;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Ships;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KadokaShipBattler.Core
{
    // Playable fixture; the enemy uses a simple scripted behavior, not Utility AI.
    public sealed class BattlePrototype : MonoBehaviour
    {
        [SerializeField] private TextAsset crewSetup;
        [SerializeField] private TextAsset ammoSetup;
        public BattleCrewDefinition CrewSetup { get; private set; }
        public BattleAmmoDefinition AmmoSetup { get; private set; }
        public AmmoDeckSpawner PlayerAmmo { get; private set; }
        public AmmoDeckSpawner EnemyAmmo { get; private set; }
        public ShipController PlayerShip { get; private set; }
        public ShipController EnemyShip { get; private set; }
        public CrewControlDirector Controls { get; private set; }
        public PlayerCrewController PlayerController => Controls.Current;
        private readonly List<PlayerCrewController> playerCrew = new();
        public IReadOnlyList<PlayerCrewController> PlayerCrew => playerCrew.AsReadOnly();
        private readonly List<CrewMember> enemyMembers = new();
        public IReadOnlyList<CrewMember> EnemyCrew => enemyMembers.AsReadOnly();
        private readonly Dictionary<string, CharacterDefinition> characterDefinitions = new();
        public CannonController PlayerCannon { get; private set; }
        public ShipCore EnemyCore { get; private set; }
        public Camera GameCamera { get; private set; }
        public bool EnemyActionsEnabled { get; set; } = true;
        public bool AllyActionsEnabled { get; set; } = true;
        public bool IsFinished => PlayerShip.IsDestroyed || EnemyShip.IsDestroyed;
        public bool PlayerWon => EnemyShip.IsDestroyed && !PlayerShip.IsDestroyed;
        private readonly List<ScriptableObject> definitions = new();
        private Sprite square;
        private CrewMember enemyCrew;
        private CrewAmmoInventory enemyInventory;
        private CannonController enemyCannon;
        private ShipCore playerCore;
        private float enemyTimer;
        private GameObject selectionMarker;

        private void Awake()
        {
            if (crewSetup == null) throw new System.InvalidOperationException("Assign the crew definition JSON to BattlePrototype.");
            CrewSetup = JsonUtility.FromJson<BattleCrewDefinition>(crewSetup.text);
            if (CrewSetup == null) throw new System.InvalidOperationException("Invalid crew definition JSON.");
            CrewSetup.Validate();
            if (ammoSetup == null) throw new System.InvalidOperationException("Assign the ammo deck JSON to BattlePrototype.");
            AmmoSetup = JsonUtility.FromJson<BattleAmmoDefinition>(ammoSetup.text);
            if (AmmoSetup == null) throw new System.InvalidOperationException("Invalid ammo deck JSON.");
            AmmoSetup.Validate();
            foreach (var stats in CrewSetup.characters)
            {
                var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
                definition.Initialize(stats);
                definitions.Add(definition);
                characterDefinitions.Add(stats.id, definition);
            }
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width,
                Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
            var cameraObject = new GameObject("Battle Camera");
            cameraObject.transform.SetParent(transform);
            GameCamera = cameraObject.AddComponent<Camera>();
            GameCamera.orthographic = true;
            GameCamera.orthographicSize = 4.8f;
            GameCamera.backgroundColor = new Color(0.025f, 0.05f, 0.11f);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Visual("Bridge", Vector2.zero, new Vector2(2, 1.3f), new Color(0.45f, 0.4f, 0.2f), 0);
            PlayerShip = CreateShip(TeamSide.Player, new Vector2(-4, 0), new Color(0.08f, 0.22f, 0.3f));
            EnemyShip = CreateShip(TeamSide.Enemy, new Vector2(4, 0), new Color(0.3f, 0.12f, 0.16f));
            playerCore = CreateCore(PlayerShip, new Vector2(-6, 1.5f));
            EnemyCore = CreateCore(EnemyShip, new Vector2(6, 1.5f));
            PlayerCannon = CreateCannon(PlayerShip, EnemyShip, new Vector2(-2, 0));
            enemyCannon = CreateCannon(EnemyShip, PlayerShip, new Vector2(2, 0));
            Controls = gameObject.AddComponent<CrewControlDirector>();
            var playerPositions = new[] { new Vector2(-5, 0), new Vector2(-4.5f, 1), new Vector2(-6, 0.5f), new Vector2(-3.5f, 1.7f), new Vector2(-6, -0.5f) };
            var enemyPositions = new[] { new Vector2(2, 0), new Vector2(5, -1.5f), new Vector2(4, 1.7f), new Vector2(3, -1.7f), new Vector2(6, -1.4f) };
            var playerFormation = CrewSetup.GetTeam(TeamSide.Player);
            var enemyFormation = CrewSetup.GetTeam(TeamSide.Enemy);
            for (var slot = 0; slot < BattleCrewDefinition.TeamSize; slot++)
            {
                var color = slot == 0 ? new Color(0.2f, 0.9f, 1) : Color.HSVToRGB(0.28f + slot * 0.07f, 0.65f, 1);
                AddPlayerCrew(playerPositions[slot], color, characterDefinitions[playerFormation[slot].id]);
                var enemy = CreateCrew(TeamSide.Enemy, enemyPositions[slot], new Color(1, 0.2f + slot * 0.1f, 0.2f), characterDefinitions[enemyFormation[slot].id]);
                enemy.gameObject.AddComponent<CrewGuardAiController>();
                enemyMembers.Add(enemy);
            }
            selectionMarker = Visual("Controlled Crew Marker", new Vector2(-5, 0.4f), new Vector2(0.2f, 0.1f), Color.white, 4);
            enemyCrew = enemyMembers[0];
            enemyInventory = enemyCrew.GetComponent<CrewAmmoInventory>();
            var ammoDefinitions = new Dictionary<string, AmmoDefinition>();
            foreach (var stats in AmmoSetup.ammo)
            {
                var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
                definition.Initialize(stats.id, stats.displayName, stats.damage, stats.weight, stats.hardness);
                definitions.Add(definition);
                ammoDefinitions.Add(stats.id, definition);
            }
            var seed = System.Environment.TickCount;
            PlayerAmmo = CreateAmmoSpawner(TeamSide.Player, AmmoSetup.player, ammoDefinitions, seed);
            EnemyAmmo = CreateAmmoSpawner(TeamSide.Enemy, AmmoSetup.enemy, ammoDefinitions, seed ^ 0x57d32);
            Debug.Log("BattlePrototype ready: five player crew and five enemy crew; 25-slot ammo decks initialized from JSON.");
        }

        private AmmoDeckSpawner CreateAmmoSpawner(TeamSide side, AmmoDeckData data,
            Dictionary<string, AmmoDefinition> ammoDefinitions, int seed)
        {
            var entries = new List<AmmoDefinition>();
            foreach (var id in data.slots) entries.Add(ammoDefinitions[id]);
            var definition = ScriptableObject.CreateInstance<AmmoDeckDefinition>();
            definition.Initialize(entries);
            definitions.Add(definition);
            var layout = AmmoSetup.GetLayout(side);
            var points = new Vector2[layout.points.Length];
            var center = side == TeamSide.Player ? new Vector2(-4, 0) : new Vector2(4, 0);
            for (var index = 0; index < points.Length; index++)
            {
                var point = layout.points[index];
                points[index] = center + new Vector2(side == TeamSide.Player ? point.x : -point.x, point.y);
                if (!CanCrewStand(side, points[index])) throw new System.ArgumentException("Ammo spawn point is outside its ship deck.");
            }
            var item = new GameObject(side + " Ammo Deck");
            item.transform.SetParent(transform);
            var spawner = item.AddComponent<AmmoDeckSpawner>();
            spawner.Initialize(definition, side, points, square, this, seed);
            return spawner;
        }

        private void AddPlayerCrew(Vector2 position, Color color, CharacterDefinition definition)
        {
            var member = CreateCrew(TeamSide.Player, position, color, definition);
            var actor = member.GetComponent<PlayerCrewController>();
            member.gameObject.AddComponent<CrewAmmoAiController>();
            playerCrew.Add(actor);
            Controls.Register(actor);
        }

        private void LateUpdate()
        {
            if (selectionMarker == null) return;
            selectionMarker.SetActive(PlayerController != null);
            if (PlayerController != null) selectionMarker.transform.position = PlayerController.transform.position + Vector3.up * 0.4f;
        }

        public bool CanPlayerStand(Vector2 point) => CanCrewStand(TeamSide.Player, point);
        public bool CanCrewStand(TeamSide side, Vector2 point)
        {
            if (point.y < -2.3f || point.y > 2.3f || point.x < -6.8f || point.x > 6.8f) return false;
            if (side == TeamSide.Enemy)
            {
                if (point.x >= 1f) return true;
                return PlayerShip.IsHullBreached && (point.x <= -1f || Mathf.Abs(point.y) <= 0.5f);
            }
            if (point.x <= -1f) return true;
            if (!EnemyShip.IsHullBreached) return false;
            return point.x >= 1f || Mathf.Abs(point.y) <= 0.5f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (IsFinished || !EnemyActionsEnabled) return;
            if (enemyCrew == null || !enemyCrew.IsAlive)
            {
                enemyCrew = enemyMembers.Find(member => member.IsAlive && member.Can(CharacterCapability.Combat) && member.Can(CharacterCapability.OperateCannon) && member.Can(CharacterCapability.CarryAmmo));
                if (enemyCrew == null) return;
                enemyInventory = enemyCrew.GetComponent<CrewAmmoInventory>();
            }
            if (PlayerShip.IsHullBreached)
            {
                var position = enemyCrew.transform.position;
                var destination = position.x > -5.5f ? new Vector3(-6, 0, 0) : playerCore.transform.position;
                enemyCrew.transform.position = Vector3.MoveTowards(position, destination, 2f * Time.deltaTime);
                if (Vector3.Distance(enemyCrew.transform.position, playerCore.transform.position) < 0.6f)
                {
                    enemyTimer += Time.deltaTime;
                    if (enemyTimer >= 1f) { playerCore.TryAttack(enemyCrew); enemyTimer = 0; }
                }
                return;
            }
            enemyTimer += Time.deltaTime;
            if (enemyTimer < 10f) return;
            enemyTimer = 0;
            if (!enemyInventory.HasAmmo && !EnemyAmmo.TrySupply(enemyInventory)) return;
            enemyCannon.TryLoadFrom(enemyInventory);
            enemyCannon.FireAt(PlayerShip);
        }

        private ShipController CreateShip(TeamSide side, Vector2 position, Color color)
        {
            var ship = Visual(side + " Deck", position, new Vector2(6, 5), color, 0).AddComponent<ShipController>();
            ship.Initialize(side);
            return ship;
        }
        private ShipCore CreateCore(ShipController owner, Vector2 position)
        {
            var core = Visual(owner.TeamSide + " Core", position, Vector2.one * 0.7f,
                new Color(0.9f, 0.4f, 1), 2).AddComponent<ShipCore>();
            core.Initialize(owner);
            return core;
        }
        private CannonController CreateCannon(ShipController owner, ShipController target, Vector2 position)
        {
            var cannon = Visual(owner.TeamSide + " Cannon", position, new Vector2(0.85f, 0.45f),
                Color.gray, 2).AddComponent<CannonController>();
            cannon.Initialize(owner, target);
            return cannon;
        }
        private CrewMember CreateCrew(TeamSide side, Vector2 position, Color color, CharacterDefinition definition)
        {
            var crew = Visual(side + " " + definition.DisplayName, position, Vector2.one * 0.45f, color, 3).AddComponent<CrewMember>();
            crew.Initialize(definition, side);
            crew.gameObject.AddComponent<CrewAmmoInventory>();
            crew.gameObject.AddComponent<PlayerCrewController>().Arena = this;
            return crew;
        }
        private GameObject Visual(string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform);
            item.transform.position = position;
            item.transform.localScale = size;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return item;
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 760, 165), "Kadoka Ship Battler - Battle Prototype");
            GUI.Label(new Rect(25, 35, 730, 25), "WASD / Arrows: move   E: pickup / load / fire   Space: attack   Tab: switch crew   R: restart");
            GUI.Label(new Rect(25, 60, 610, 25), $"Player hull {PlayerShip.CurrentHull:0} / core {PlayerShip.CurrentCore:0}     Enemy hull {EnemyShip.CurrentHull:0} / core {EnemyShip.CurrentCore:0}");
            if (PlayerController != null)
            {
                var inventory = PlayerController.GetComponent<CrewAmmoInventory>();
                var member = PlayerController.GetComponent<CrewMember>();
                GUI.Label(new Rect(25, 85, 730, 25), $"Control: {member.Definition.DisplayName} HP {member.CurrentHp:0}/{member.Definition.MaxHp:0}   Carry: {inventory.Count}/{inventory.MaxCarryCount}   Weight: {inventory.CurrentWeight:0.#}/{inventory.CarryCapacity:0.#}   Loaded: {PlayerCannon.IsLoaded}");
            }
            GUI.Label(new Rect(25, 110, 730, 25), $"White marker: controlled crew. Yellow ammo: weight 2. Orange ammo: weight 3. Bridge: {(EnemyShip.IsHullBreached ? "OPEN" : "LOCKED")}");
            GUI.Label(new Rect(25, 135, 730, 25), $"Deck waiting: Player {PlayerAmmo.Deck.WaitingCount}/25   Enemy {EnemyAmmo.Deck.WaitingCount}/25   Ground ammo expires after 60s");
            GUI.Label(new Rect(10, 185, 800, 30), IsFinished ? (PlayerWon ? "VICTORY - enemy core destroyed" : "DEFEAT - player core destroyed") :
                "Yellow: ammo   Gray: cannon   Purple: core. Breach the hull, cross the bridge, then attack the core.");
        }
        private void OnDestroy()
        {
            foreach (var definition in definitions) if (definition != null) Destroy(definition);
            if (square != null) Destroy(square);
        }
    }
}
