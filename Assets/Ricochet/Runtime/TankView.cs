// The original's tank (U.RICO-T45, docs/BIBLE.md "Tanks"): spr_tank_bottom's hull (obj_bottom) under spr_tank_top's
// turret (obj_player), both tinted the seat's colour as setup_players' image_blend = pColor[i] did, the turret on the
// aim, the hull turning after it a degree a step while the tank drives, and spr_tracks prints (obj_tracks) laid under the
// hull every five steps of driving that hold at 0.7 and then fade. The step rules are Core's TankMotion; this only draws
// them, on every peer, from what the player has synced (its spot, its aim and whether it is driving).
using System.Collections.Generic;
using Ricochet.Core.Art;
using UnityEngine;

namespace Ricochet
{
    public sealed class TankView : MonoBehaviour
    {
        /// <summary>obj_tracks' depth 2: under the hull (depth 1) and the turret (depth 0), over the floor.</summary>
        public const int TracksOrder = -4;
        public const int HullOrder = 0;
        public const int TurretOrder = 1;

        // The most steps one frame catches up on (a long hitch skips the rest rather than stalling).
        private const int MaxStepsPerFrame = 30;

        private sealed class Print
        {
            public SpriteRenderer Renderer;
            public float Steps; // since it was laid
        }

        private SpriteRenderer _hull;
        private SpriteRenderer _turret;
        private Sprite _trackSprite;
        private readonly TrackLayer _layer = new TrackLayer();
        private double _hullAngle = double.NaN;
        private float _clock;
        private int _levelVersion = -1;
        private Transform _tracksRoot;
        private readonly List<Print> _prints = new List<Print>();
        private readonly Stack<SpriteRenderer> _spare = new Stack<SpriteRenderer>();

        /// <summary>The hull's angle in degrees (anticlockwise from +x, as the aim is).</summary>
        public float HullAngle => double.IsNaN(_hullAngle) ? 0f : (float)_hullAngle;

        /// <summary>The turret's angle in degrees.</summary>
        public float TurretAngle => _turret != null ? _turret.transform.localEulerAngles.z : 0f;

        public SpriteRenderer Hull => _hull;
        public SpriteRenderer Turret => _turret;

        /// <summary>The prints this tank has left that have not faded away yet.</summary>
        public int TrackCount => _prints.Count;

        /// <summary>The alpha of the newest print (0 when there is none).</summary>
        public float NewestTrackAlpha => _prints.Count > 0 ? _prints[_prints.Count - 1].Renderer.color.a : 0f;

        /// <summary>
        /// Gives <paramref name="player"/> its tank (again, when the kit respawns the object), or returns null, leaving the
        /// player its code-drawn squares, when the art catalogue lacks the tank sprites.
        /// </summary>
        public static TankView Create(Transform player, string playerName, float parentScale)
        {
            Sprite top = GameArt.Frame(GmlSprites.TankTop);
            Sprite bottom = GameArt.Frame(GmlSprites.TankBottom);
            if (top == null || bottom == null)
            {
                return null;
            }

            Transform existing = player.Find("Tank");
            GameObject tank = existing != null ? existing.gameObject : new GameObject("Tank");
            tank.transform.SetParent(player, false);
            // The sprites are imported at the original's pixel size; undo the player's own scale so the tank draws at it
            // (a shrink ray's Scale still shrinks it).
            tank.transform.localScale = Vector3.one / Mathf.Max(0.01f, parentScale);
            TankView view = tank.GetComponent<TankView>();
            if (view == null)
            {
                view = tank.AddComponent<TankView>();
            }

            view._hull = view.Part("Hull", bottom, HullOrder);
            view._turret = view.Part("Turret", top, TurretOrder);
            view._trackSprite = GameArt.Frame(GmlSprites.Tracks);
            if (view._tracksRoot == null)
            {
                // The prints stay where they were laid, so they live in a root of their own, not under the moving tank.
                view._tracksRoot = new GameObject("Tracks (" + playerName + ")").transform;
            }

            return view;
        }

        private SpriteRenderer Part(string partName, Sprite sprite, int order)
        {
            Transform existing = transform.Find(partName);
            GameObject part = existing != null ? existing.gameObject : new GameObject(partName);
            part.transform.SetParent(transform, false);
            SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = part.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>
        /// Draws the tank this frame: the turret on <paramref name="turretHeading"/>, both parts in
        /// <paramref name="color"/>, and, at the room speed, the hull's turn and the prints while <paramref name="driving"/>
        /// (the authority's held drive key: move_forward / move_backward's isMoving). A new level (its
        /// <paramref name="levelVersion"/>) sweeps the old level's prints away, as the original's room change did.
        /// </summary>
        public void Show(float turretHeading, bool driving, Color color, int levelVersion, float deltaTime)
        {
            if (_hull == null || _turret == null)
            {
                return;
            }

            if (double.IsNaN(_hullAngle))
            {
                _hullAngle = TankMotion.Wrap(turretHeading); // setup_players: bottom[i].image_angle = player[i].image_angle
            }

            if (levelVersion != _levelVersion)
            {
                _levelVersion = levelVersion;
                ClearTracks();
            }

            float roomSpeed = GameArt.Catalogue != null ? GameArt.Catalogue.RoomSpeed : 60f;
            float stepSeconds = 1f / roomSpeed;
            _clock += Mathf.Max(0f, deltaTime);
            int steps = 0;
            while (_clock >= stepSeconds && steps < MaxStepsPerFrame)
            {
                _clock -= stepSeconds;
                steps++;
                if (driving)
                {
                    _hullAngle = TankMotion.TurnHull(_hullAngle, turretHeading);
                }

                if (_layer.Step(driving))
                {
                    Lay();
                }
            }

            if (steps == MaxStepsPerFrame)
            {
                _clock = 0f;
            }

            Age(steps);

            _turret.transform.localRotation = Quaternion.Euler(0f, 0f, turretHeading);
            _hull.transform.localRotation = Quaternion.Euler(0f, 0f, (float)_hullAngle);
            _turret.color = color;
            _hull.color = color;
        }

        // spawn_tracks: a print under the hull, turned as the hull is.
        private void Lay()
        {
            if (_trackSprite == null || _tracksRoot == null)
            {
                return;
            }

            SpriteRenderer renderer;
            if (_spare.Count > 0)
            {
                renderer = _spare.Pop();
                renderer.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Print");
                go.transform.SetParent(_tracksRoot, false);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _trackSprite;
                renderer.sortingOrder = TracksOrder;
            }

            renderer.transform.SetPositionAndRotation(new Vector3(transform.position.x, transform.position.y, 0f),
                Quaternion.Euler(0f, 0f, (float)_hullAngle));
            renderer.color = new Color(1f, 1f, 1f, (float)TankMotion.TrackAlpha);
            _prints.Add(new Print { Renderer = renderer, Steps = 0f });
        }

        private void Age(int steps)
        {
            if (steps <= 0)
            {
                return;
            }

            for (int i = _prints.Count - 1; i >= 0; i--)
            {
                Print print = _prints[i];
                print.Steps += steps;
                if (print.Steps >= TankMotion.TrackLifeSteps)
                {
                    Retire(i);
                    continue;
                }

                Color c = print.Renderer.color;
                c.a = (float)TankMotion.TrackAlphaAt(print.Steps);
                print.Renderer.color = c;
            }
        }

        private void Retire(int index)
        {
            SpriteRenderer renderer = _prints[index].Renderer;
            _prints.RemoveAt(index);
            if (renderer != null)
            {
                renderer.gameObject.SetActive(false);
                _spare.Push(renderer);
            }
        }

        /// <summary>Takes every print away at once.</summary>
        public void ClearTracks()
        {
            for (int i = _prints.Count - 1; i >= 0; i--)
            {
                Retire(i);
            }
        }

        private void OnDestroy()
        {
            DestroyTracks();
        }

        /// <summary>Destroys the prints and their root (the player is going away; the tank itself goes with the player).</summary>
        public void DestroyTracks()
        {
            _prints.Clear();
            _spare.Clear();
            if (_tracksRoot != null)
            {
                Destroy(_tracksRoot.gameObject);
                _tracksRoot = null;
            }
        }
    }
}
