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
        public ShipController PlayerShip { get; private set; }
        public ShipController EnemyShip { get; private set; }
        public CrewControlDirector Controls { get; private set; }
        public PlayerCrewController PlayerController => Controls.Current;
        private readonly List<PlayerCrewController> playerCrew = new();
        public IReadOnlyList<PlayerCrewController> PlayerCrew => playerCrew.AsReadOnly();
        public CannonController PlayerCannon { get; private set; }
        public ShipCore EnemyCore { get; private set; }
        public Camera GameCamera { get; private set; }
        public bool EnemyActionsEnabled { get; set; } = true;
        public bool AllyActionsEnabled { get; set; } = true;
        public bool IsFinished => PlayerShip.IsDestroyed || EnemyShip.IsDestroyed;
        public bool PlayerWon => EnemyShip.IsDestroyed && !PlayerShip.IsDestroyed;
        private readonly List<ScriptableObject> definitions = new();
        private Sprite square;
        private AmmoDefinition ammo;
        private CrewMember enemyCrew;
        private CrewAmmoInventory enemyInventory;
        private CannonController enemyCannon;
        private ShipCore playerCore;
        private float enemyTimer;
        private GameObject selectionMarker;

        private void Awake()
        {
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
            AddPlayerCrew(new Vector2(-5, 0), new Color(0.2f, 0.9f, 1), "Leader", 4, 5);
            AddPlayerCrew(new Vector2(-4.5f, 1), new Color(0.3f, 1, 0.55f), "Gunner", 2.8f, 8);
            selectionMarker = Visual("Controlled Crew Marker", new Vector2(-5, 0.4f), new Vector2(0.2f, 0.1f), Color.white, 4);
            enemyCrew = CreateCrew(TeamSide.Enemy, new Vector2(2, 0), new Color(1, 0.3f, 0.3f));
            enemyInventory = enemyCrew.GetComponent<CrewAmmoInventory>();
            ammo = ScriptableObject.CreateInstance<AmmoDefinition>();
            ammo.Initialize("prototype-ammo", "Cannonball", 25f);
            definitions.Add(ammo);
            for (var i = 0; i < 8; i++)
            {
                var pickup = Visual("Ammo " + (i + 1), new Vector2(-5 + (i % 4) * 0.65f,
                    -1.2f - (i / 4) * 0.65f), Vector2.one * 0.3f, new Color(1, 0.8f, 0.2f), 2)
                    .AddComponent<AmmoPickup>();
                pickup.Initialize(ammo);
            }
            Debug.Log("BattlePrototype ready: two ships, eight ammo pickups, crew switching and enemy behavior initialized.");
        }

        private void AddPlayerCrew(Vector2 position, Color color, string name, float speed, float combat)
        {
            var member = CreateCrew(TeamSide.Player, position, color, name, speed, combat);
            var actor = member.gameObject.AddComponent<PlayerCrewController>();
            actor.Arena = this;
            member.gameObject.AddComponent<CrewAmmoAiController>();
            playerCrew.Add(actor);
            Controls.Register(actor);
        }

        private void LateUpdate()
        {
            if (selectionMarker != null && PlayerController != null)
                selectionMarker.transform.position = PlayerController.transform.position + Vector3.up * 0.4f;
        }

        public bool CanPlayerStand(Vector2 point)
        {
            if (point.y < -2.3f || point.y > 2.3f || point.x < -6.8f || point.x > 6.8f) return false;
            if (point.x <= -1f) return true;
            if (!EnemyShip.IsHullBreached) return false;
            return point.x >= 1f || Mathf.Abs(point.y) <= 0.5f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (IsFinished || !EnemyActionsEnabled) return;
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
            enemyInventory.TryPickup(ammo);
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
        private CrewMember CreateCrew(TeamSide side, Vector2 position, Color color, string name = "Crew", float speed = 4, float combat = 5)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.Initialize(side + "-" + name, name, CharacterCapability.CarryAmmo |
                CharacterCapability.OperateCannon | CharacterCapability.Combat | CharacterCapability.BoardEnemyShip, speed, combat);
            definitions.Add(definition);
            var crew = Visual(side + " Crew", position, Vector2.one * 0.45f, color, 3).AddComponent<CrewMember>();
            crew.Initialize(definition, side);
            crew.gameObject.AddComponent<CrewAmmoInventory>();
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
            GUI.Box(new Rect(10, 10, 760, 140), "Kadoka Ship Battler - Battle Prototype");
            GUI.Label(new Rect(25, 35, 730, 25), "WASD / Arrows: move   E: pickup / load / fire   Space: attack core   Tab: switch crew   R: restart");
            GUI.Label(new Rect(25, 60, 610, 25), $"Player hull {PlayerShip.CurrentHull:0} / core {PlayerShip.CurrentCore:0}     Enemy hull {EnemyShip.CurrentHull:0} / core {EnemyShip.CurrentCore:0}");
            if (PlayerController != null)
                GUI.Label(new Rect(25, 85, 730, 25), $"Control: {PlayerController.GetComponent<CrewMember>().Definition.DisplayName}   Carrying: {PlayerController.GetComponent<CrewAmmoInventory>().HasAmmo}   Loaded: {PlayerCannon.IsLoaded}   Bridge: {(EnemyShip.IsHullBreached ? "OPEN" : "LOCKED")}");
            GUI.Label(new Rect(25, 110, 730, 25), "White marker: controlled crew. Released crew resumes its ammo AI from its current position.");
            GUI.Label(new Rect(10, 160, 800, 30), IsFinished ? (PlayerWon ? "VICTORY - enemy core destroyed" : "DEFEAT - player core destroyed") :
                "Yellow: ammo   Gray: cannon   Purple: core. Breach the hull, cross the bridge, then attack the core.");
        }
        private void OnDestroy()
        {
            foreach (var definition in definitions) if (definition != null) Destroy(definition);
            if (square != null) Destroy(square);
        }
    }
}
