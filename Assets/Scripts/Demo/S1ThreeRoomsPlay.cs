using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueShooter.Ai;
using RogueShooter.Balance;
using RogueShooter.Build;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Play driver for the delivered three-room scene only.
    /// Rooms stay 28×22. Closed arches stay scale 1. Stage1Maze is not touched.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class S1ThreeRoomsPlay : MonoBehaviour
    {
        const string SceneName = "S1ThreeRooms_TurnCorridor";
        const float InteractReach = 2.2f;

        Transform _player;
        PlayerCharge _charge;
        PlayerMotor2D _motor;
        Camera _cam;
        SpriteRenderer _east;
        SpriteRenderer _chestGate;
        SpriteRenderer _altarGate;
        Transform _chestFocus;
        Transform _altarFocus;
        readonly List<StubEnemy> _normal = new List<StubEnemy>();
        readonly List<StubEnemy> _chest = new List<StubEnemy>();
        RunBuildState _build;
        RewardScreenView _reward;
        AltarPick[] _chestOffers = System.Array.Empty<AltarPick>();
        AltarPick[] _altarOffers = System.Array.Empty<AltarPick>();
        bool _offerIsAltar;
        bool _chestTaken;
        bool _altarTaken;
        bool _chestEngaged;
        bool _eastOpen;
        bool _chestDoorOpen;
        bool _altarDoorOpen;
        Vector3 _lastGood;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != SceneName)
                return;
            if (Object.FindObjectOfType<S1ThreeRoomsPlay>() != null)
                return;
            var go = new GameObject("S1ThreeRoomsPlay");
            go.AddComponent<S1ThreeRoomsPlay>();
        }

        void Start()
        {
            RunPause.InteractOpen = false;
            RunPause.RunSettled = false;
            Time.timeScale = 1f;
            if (!BindScene())
            {
                Debug.Log("[S1Three] FAIL missing archer or a gate");
                return;
            }

            _build = new RunBuildState(0.45f, 0.55f, 0);
            _charge.BindOwnedRewards(_build.OwnedRewardIds);
            SpawnNormal();
            SpawnChest();
            RefreshDoors();
            _lastGood = _player.position;
            Debug.Log("[S1Three] ready scene=" + SceneName
                      + " speed=" + _motor.BaseSpeed.ToString("0.00")
                      + " ortho=" + _cam.orthographicSize.ToString("0.00")
                      + " scale=" + _player.localScale.x.ToString("0.00")
                      + " gate=" + _east.transform.localScale.x.ToString("0.00")
                      + " normal=" + Alive(_normal)
                      + " chest=" + Alive(_chest));
        }

        bool BindScene()
        {
            Transform archer = FindNamed("Archer scale 1");
            _east = Gate("Gate normal east");
            _chestGate = Gate("Gate chest south");
            _altarGate = Gate("Gate altar south");
            _chestFocus = FindNamed("Chest focus");
            _altarFocus = FindNamed("Altar focus");
            if (archer == null || _east == null || _chestGate == null || _altarGate == null)
                return false;

            _player = archer;
            _player.localScale = Vector3.one;
            Stage1IsoActor actor = _player.GetComponent<Stage1IsoActor>();
            if (actor == null)
                actor = _player.gameObject.AddComponent<Stage1IsoActor>();
            actor.Bind("archer");
            _player.localScale = Vector3.one;
            SpriteRenderer body = _player.GetComponent<SpriteRenderer>();
            if (body != null)
                body.sortingOrder = 20;

            _motor = _player.GetComponent<PlayerMotor2D>();
            if (_motor == null)
                _motor = _player.gameObject.AddComponent<PlayerMotor2D>();
            _motor.Configure(MazeRules.PlayMoveSpeed);

            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            if (vitals == null)
                vitals = _player.gameObject.AddComponent<PlayerVitals>();
            vitals.Configure(EnemyDamageCatalog.PlayerMaxHpRef);

            PlayerStrike strike = _player.GetComponent<PlayerStrike>();
            if (strike == null)
                strike = _player.gameObject.AddComponent<PlayerStrike>();
            strike.Configure(1.85f);

            _charge = _player.GetComponent<PlayerCharge>();
            if (_charge == null)
                _charge = _player.gameObject.AddComponent<PlayerCharge>();
            _charge.BindOwnedRewards(_build != null ? _build.OwnedRewardIds : null);
            if (_player.GetComponent<PlayerRoll>() == null)
                _player.gameObject.AddComponent<PlayerRoll>();
            if (_player.GetComponent<GuaranteedCritActive>() == null)
                _player.gameObject.AddComponent<GuaranteedCritActive>();

            _cam = Camera.main;
            if (_cam == null)
                return false;
            _cam.orthographic = true;
            CameraViewService view = _cam.GetComponent<CameraViewService>();
            if (view == null)
                view = _cam.gameObject.AddComponent<CameraViewService>();
            view.Configure(MazeRules.PlayOrtho);
            CameraFollow2D follow = _cam.GetComponent<CameraFollow2D>();
            if (follow == null)
                follow = _cam.gameObject.AddComponent<CameraFollow2D>();
            follow.SetFaceOn(16f);
            follow.SetTarget(_player);
            return true;
        }

        void SpawnNormal()
        {
            Spawn(_normal, EnemyKindIds.Normal, new Vector3(1.7f, -3f, 0f), true);
            Spawn(_normal, EnemyKindIds.Dog, new Vector3(-6f, 4f, 0f), true);
            Spawn(_normal, EnemyKindIds.CultMage, new Vector3(6f, 5f, 0f), true);
        }

        void SpawnChest()
        {
            Spawn(_chest, EnemyKindIds.Normal, new Vector3(30f, 40f, 0f), false);
            Spawn(_chest, EnemyKindIds.Dog, new Vector3(42f, 36f, 0f), false);
        }

        StubEnemy Spawn(List<StubEnemy> into, string kind, Vector3 pos, bool aiOn)
        {
            var go = new GameObject("S1Three_" + kind);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one;
            string family = kind == EnemyKindIds.Dog ? "dog"
                : kind == EnemyKindIds.CultMage ? "mage"
                : "skel";
            Stage1IsoActor actor = go.AddComponent<Stage1IsoActor>();
            actor.Bind(family);
            go.transform.localScale = Vector3.one;
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = 20;
            StubEnemy enemy = go.AddComponent<StubEnemy>();
            enemy.ConfigureKind(kind, SpawnWaveCatalog.BaseHp(kind), false);
            MobFourStateAi ai = go.AddComponent<MobFourStateAi>();
            ai.Configure(null, _player, StageId.S1, true, EnemyKindCatalog.HitDamageStub(kind), false);
            ai.enabled = aiOn;
            enemy.Died += dead =>
            {
                MobFourStateAi brain = dead.GetComponent<MobFourStateAi>();
                if (brain != null)
                    brain.enabled = false;
            };
            into.Add(enemy);
            return enemy;
        }

        void Update()
        {
            if (_player == null)
                return;
            if (_reward != null && _reward.Session != null && _reward.Session.Open)
            {
                HandleRewardKeys();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
        }

        void LateUpdate()
        {
            if (_player == null)
                return;
            if (ChestInterior(_player.position))
                _chestEngaged = true;
            if (_chestEngaged)
                Wake(_chest);
            RefreshDoors();
            ClampPlayer();
            ClampGroup(_normal, false);
            ClampGroup(_chest, true);
        }

        void RefreshDoors()
        {
            bool normalClear = Alive(_normal) == 0;
            bool chestClear = Alive(_chest) == 0;
            _eastOpen = normalClear;
            _chestDoorOpen = normalClear && (!_chestEngaged || chestClear);
            _altarDoorOpen = normalClear && chestClear;
            ShowGate(_east, _eastOpen);
            ShowGate(_chestGate, _chestDoorOpen);
            ShowGate(_altarGate, _altarDoorOpen);
        }

        static void ShowGate(SpriteRenderer gate, bool open)
        {
            if (gate == null)
                return;
            gate.transform.localScale = Vector3.one;
            gate.enabled = !open;
        }

        void ClampPlayer()
        {
            if (Allowed(_player.position))
            {
                _lastGood = _player.position;
                return;
            }

            _player.position = _lastGood;
        }

        void ClampGroup(List<StubEnemy> group, bool chestRoom)
        {
            for (int i = 0; i < group.Count; i++)
            {
                StubEnemy enemy = group[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                Vector3 p = enemy.transform.position;
                bool inside = chestRoom ? ChestInterior(p) : NormalInterior(p);
                if (!inside)
                    enemy.transform.position = chestRoom
                        ? new Vector3(36f, 39f, 0f)
                        : new Vector3(0f, 0f, 0f);
            }
        }

        bool Allowed(Vector3 p)
        {
            float x = p.x;
            float y = p.y;
            if (Box(-13.2f, -10.2f, _eastOpen ? 14.6f : 13.1f, 10.2f, x, y))
                return true;
            if (!_eastOpen)
                return false;
            if (Box(14f, -3.3f, 39.2f, 1.3f, x, y))
                return true;
            if (Box(32.8f, 1.3f, 39.2f, 6.6f, x, y))
                return true;
            if (Box(32.8f, 6f, 39.2f, _chestDoorOpen ? 27.6f : 27.15f, x, y))
                return true;
            if (!_chestDoorOpen && !ChestInterior(p))
                return false;
            if (_chestDoorOpen && Box(34.6f, 27.2f, 37.4f, 29.4f, x, y))
                return true;
            if (ChestInterior(p))
                return true;
            if (!_altarDoorOpen)
                return false;
            if (Box(34.6f, 47f, 37.4f, 51.4f, x, y))
                return true;
            return AltarInterior(p);
        }

        static bool NormalInterior(Vector3 p)
        {
            return Box(-13.2f, -10.2f, 13.1f, 10.2f, p.x, p.y);
        }

        static bool ChestInterior(Vector3 p)
        {
            return Box(23.2f, 29f, 48.8f, 47.2f, p.x, p.y);
        }

        static bool AltarInterior(Vector3 p)
        {
            return Box(23.2f, 50.6f, 48.8f, 69.2f, p.x, p.y);
        }

        static bool Box(float x0, float y0, float x1, float y1, float x, float y)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        static void Wake(List<StubEnemy> group)
        {
            for (int i = 0; i < group.Count; i++)
            {
                StubEnemy enemy = group[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                MobFourStateAi ai = enemy.GetComponent<MobFourStateAi>();
                if (ai != null)
                    ai.enabled = true;
            }
        }

        static int Alive(List<StubEnemy> group)
        {
            int n = 0;
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] != null && !group[i].IsDead)
                    n++;
            }

            return n;
        }

        void TryInteract()
        {
            if (_player == null || _chestFocus == null || _altarFocus == null)
                return;
            if (Vector2.Distance(_player.position, _chestFocus.position) <= InteractReach)
            {
                if (Alive(_chest) > 0)
                {
                    Debug.Log("[S1Three] clear the room first");
                    return;
                }

                OpenChest();
                return;
            }

            if (Vector2.Distance(_player.position, _altarFocus.position) <= InteractReach)
            {
                if (!_altarDoorOpen)
                {
                    Debug.Log("[S1Three] door still shut");
                    return;
                }

                OpenAltar();
            }
        }

        void OpenChest()
        {
            if (_chestTaken)
                return;
            if (_chestOffers.Length == 0)
                _chestOffers = AltarRewardRoll.RollThreeChest(false, new System.Random(42));
            _offerIsAltar = false;
            RewardView().ShowChest(Cards(_chestOffers));
            Debug.Log("[S1Three] chest offer n=" + _chestOffers.Length);
        }

        void OpenAltar()
        {
            if (_altarTaken)
                return;
            if (_altarOffers.Length == 0)
                _altarOffers = AltarRewardRoll.RollThree(AltarSize.Small, new System.Random(42));
            _offerIsAltar = true;
            RewardView().ShowAltar(Cards(_altarOffers));
            Debug.Log("[S1Three] altar offer n=" + _altarOffers.Length);
        }

        RewardScreenView RewardView()
        {
            if (_reward == null)
            {
                _reward = GetComponent<RewardScreenView>();
                if (_reward == null)
                    _reward = gameObject.AddComponent<RewardScreenView>();
                _reward.OnPick = ConfirmReward;
            }

            return _reward;
        }

        static RewardCardData[] Cards(AltarPick[] picks)
        {
            var cards = new RewardCardData[picks.Length];
            for (int i = 0; i < picks.Length; i++)
                cards[i] = RewardPresent.ToCard(picks[i].Id, picks[i].Tier, "", "", i, false);
            return cards;
        }

        void HandleRewardKeys()
        {
            if (_reward == null || _reward.Session == null || !_reward.ChoicesVisible)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseReward();
                return;
            }

            int pick = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pick = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pick = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pick = 2;
            if (pick >= 0)
                ConfirmReward(pick);
        }

        void ConfirmReward(int pick)
        {
            if (_reward == null || !_reward.ChoicesVisible)
                return;
            AltarPick[] offers = _offerIsAltar ? _altarOffers : _chestOffers;
            if (offers == null || pick < 0 || pick >= offers.Length)
                return;
            AltarPick chosen = offers[pick];
            if (!RewardCatalog.TryGet(chosen.Id, out RewardRow row))
                return;
            if (_offerIsAltar)
            {
                _build.ConfirmAltarPick(AltarSize.Small, chosen.Id);
                _altarTaken = true;
            }
            else
            {
                string rarity = row.Tier == RewardTier.High ? "高" : row.Tier == RewardTier.Mid ? "中" : "低";
                _build.GrantBuildPick(chosen.Id, rarity, 0, row.BuildEquiv);
                _build.AddGold(EconomyGold.ChestGold(false, 0f));
                _chestTaken = true;
            }

            ApplyOwned(row);
            _charge.BindOwnedRewards(_build.OwnedRewardIds);
            Debug.Log("[S1Three] picked " + chosen.Id);
            CloseReward();
        }

        void ApplyOwned(RewardRow picked)
        {
            var ids = _build.OwnedRewardIds;
            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            if (vitals != null)
            {
                vitals.ApplyRewardMax(RewardStatHooks.ProductMul(ids, "max_hp"));
                if (picked.Stat == "heal" && picked.Value > 0f)
                    vitals.Heal(vitals.MaxHp * picked.Value);
            }

            if (_motor != null)
                _motor.ApplyRewardSpeed(RewardStatHooks.ProductMul(ids, "move_speed"));
        }

        void CloseReward()
        {
            if (_reward != null)
                _reward.Hide();
            RunPause.InteractOpen = false;
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            GUI.color = new Color(1f, 1f, 1f, 0.86f);
            GUI.Box(new Rect(16f, Screen.height - 36f, 520f, 24f), "WASD   按住鼠标或 C 射击   清完开门   E 开箱或祭坛");
        }

        static Transform FindNamed(string name)
        {
            Transform[] all = Object.FindObjectsOfType<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                    return all[i];
            }

            return null;
        }

        static SpriteRenderer Gate(string name)
        {
            Transform t = FindNamed(name);
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }
    }
}
