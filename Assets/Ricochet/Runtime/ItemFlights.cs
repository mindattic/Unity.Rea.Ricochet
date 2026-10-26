// The items in flight (U.RICO-T79, docs/BIBLE.md "Item flights and endgame flashes"), on every peer: a teleporter, a shrink
// ray and a redirector fly the path the server flew them along (ItemUsed's Flight; Player's ItemFlown carries it to the
// clients, packed by Core's ShotWire), at their multiple of the shot speed (Core's ItemLooks):
//   Teleporter   obj_teleporter's spr_teleporter playing at image_speed 1, turned to the aim; teleport_player's small
//                white ring where the shooter stood as it sets off, and where the shooter lands a medium ring in white and
//                one in the shooter's colour and a small white firework.
//   Shrink ray   an ef_spark in lime every step where it is (obj_shrinkray's spr_shrinkball is never drawn: its Draw is
//                commented out, as is its obj_shrinkray_child); striking a player, a medium lime firework.
//   Redirector   obj_redirector flying: spr_redirector_arrow from 0.15 of its size growing to 0.30, turning 3 degrees a step
//                and set back to its heading at every bounce. The standing redirector (LevelHazard) waits for it to land.
// The items take effect as the server uses them (the tank is already where it teleports to, the player already shrunk);
// what is drawn here is presentation only. A new level sweeps every flight away.
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed class ItemFlights : MonoBehaviour
    {
        /// <summary>Over the tanks (0 to 2) and the turn guide, under the glass and the effects.</summary>
        public const int Order = 4;

        public const string TeleporterSprite = "spr_teleporter";

        private static ItemFlights s_instance;

        private readonly List<Flight> _flights = new List<Flight>();
        private (int Version, int Map, int Seed) _level;

        /// <summary>Items in the air now (for tests).</summary>
        public static int Flying => s_instance != null ? s_instance._flights.Count : 0;

        /// <summary>The scene's flights, made the first time something asks.</summary>
        public static ItemFlights Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new GameObject("Item flights").AddComponent<ItemFlights>();
                }

                return s_instance;
            }
        }

        /// <summary>Makes sure the flights are listening to the feed (the game calls it as a player spawns).</summary>
        public static void Ensure() => _ = Instance;

        /// <summary>How many of <paramref name="item"/> are in the air now (for tests).</summary>
        public static int Count(Item item)
        {
            int count = 0;
            if (s_instance != null)
            {
                foreach (Flight flight in s_instance._flights)
                {
                    count += flight.Item == item ? 1 : 0;
                }
            }

            return count;
        }

        /// <summary>
        /// True while a redirector seat <paramref name="seat"/> launched is still in the air (any seat's when it is -1): the
        /// standing redirector it becomes waits for it to land.
        /// </summary>
        public static bool RedirectorInFlight(int seat)
        {
            if (s_instance == null)
            {
                return false;
            }

            foreach (Flight flight in s_instance._flights)
            {
                if (flight.Item == Item.Redirector && (seat < 0 || flight.Seat == seat))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The sprite drawn for the first <paramref name="item"/> in the air (for tests; null for none).</summary>
        public static SpriteRenderer RendererOf(Item item)
        {
            if (s_instance != null)
            {
                foreach (Flight flight in s_instance._flights)
                {
                    if (flight.Item == item)
                    {
                        return flight.Renderer;
                    }
                }
            }

            return null;
        }

        /// <summary>Takes every flight away (a new level, a test tearing down).</summary>
        public static void Clear()
        {
            if (s_instance != null)
            {
                s_instance.ClearAll();
            }
        }

        /// <summary>Flies <paramref name="item"/> along <paramref name="path"/> for <paramref name="who"/> (null for nobody): what the feed's ItemUsed does.</summary>
        public static void Launch(Player who, Item item, ShotPath path) => Instance.Add(who, item, path);

        private void OnEnable()
        {
            Presentation.Raised += OnEvent;
            _level = (Arena.LevelVersion, Arena.MapIndex, Arena.LevelSeed);
        }

        private void OnDisable()
        {
            Presentation.Raised -= OnEvent;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void OnEvent(Player who, Cmd.GameEvent e)
        {
            if (e is Cmd.ItemUsed used && used.Flight != null)
            {
                Add(who, used.Item, used.Flight);
            }
        }

        private void Add(Player who, Item item, ShotPath path)
        {
            if (path == null || !ItemLooks.Flies(item))
            {
                return;
            }

            var flight = new Flight
            {
                Item = item,
                Path = path,
                Who = who,
                Seat = who != null ? who.Seat : -1,
                Color = who != null ? (Color)who.Color : Color.white,
            };

            switch (item)
            {
                case Item.Teleporter:
                    flight.Renderer = MakeSprite("Teleporter", GameArt.Frame(TeleporterSprite));
                    // teleport_player: ef_ring (small, white) where the shooter stood.
                    GmlParticles.Play(GmEffect.Ring, Arena.ToUnity(path.Origin), ItemLooks.TeleportLeaveRingSize, Color.white);
                    break;
                case Item.ShrinkRay:
                    // obj_shrinkray's Draw is //draw_self() and shoot_shrinkray's obj_shrinkray_child is commented out:
                    // the original shows the ray by its sparks alone, and so does this.
                    break;
                case Item.Redirector:
                    flight.Renderer = MakeSprite("Redirector", GameArt.Frame(GmlSprites.RedirectorArrow));
                    break;
            }

            _flights.Add(flight);
            Step(flight, 0f);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite)
        {
            if (sprite == null)
            {
                return null; // no art built: the flight still runs (its landing still plays), with nothing drawn
            }

            SpriteRenderer renderer = Steps.Sprite(transform, name, Order);
            renderer.sprite = sprite;
            return renderer;
        }

        private void Update()
        {
            var level = (Arena.LevelVersion, Arena.MapIndex, Arena.LevelSeed);
            if (level != _level)
            {
                _level = level;
                ClearAll(); // a new round or level: clear_objects
            }

            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                Flight flight = _flights[i];
                if (Step(flight, Time.deltaTime))
                {
                    Land(flight);
                    Remove(flight);
                    _flights.RemoveAt(i);
                }
            }
        }

        // Moves one flight on by `seconds`; true once it has reached the end of its path.
        private static bool Step(Flight flight, float seconds)
        {
            double speed = ItemLooks.SpeedFactor(flight.Item) * Arena.Rules.Speed; // units a second
            float stepsBefore = flight.Steps;
            flight.Age += seconds;
            flight.Steps += seconds * Steps.PerSecond;
            flight.Distance += speed * seconds;
            ShotPath path = flight.Path;
            Vector2 at = Arena.ToUnity(path.PositionAt(flight.Distance, out _));

            switch (flight.Item)
            {
                case Item.Teleporter:
                    if (flight.Renderer != null)
                    {
                        Sprite frame = GameArt.FrameAt(TeleporterSprite, flight.Age, true, (float)ItemLooks.TeleporterImageSpeed);
                        if (frame != null)
                        {
                            flight.Renderer.sprite = frame;
                        }

                        // shoot_teleporter: image_angle = the aim, set once (it never turns in flight).
                        Place(flight.Renderer, at, (float)path.Heading, 1f);
                    }

                    break;
                case Item.ShrinkRay:
                    // obj_shrinkray's Step: an ef_spark (small, lime) where it is, every step.
                    for (int s = Mathf.FloorToInt(stepsBefore); s < Mathf.FloorToInt(flight.Steps); s++)
                    {
                        GmlParticles.Play(GmEffect.Spark, at, ItemLooks.ShrinkSparkSize, Steps.Of(ItemLooks.Lime, 1f));
                    }

                    break;
                case Item.Redirector:
                {
                    // redirector_step, flying: image_angle += 3 a step, set back to its direction at each bounce.
                    double segmentHeading = path.Heading;
                    double segmentStart = 0;
                    foreach (Contact contact in path.Contacts)
                    {
                        if (contact.Distance > flight.Distance)
                        {
                            break;
                        }

                        segmentHeading = contact.HeadingOut;
                        segmentStart = contact.Distance;
                    }

                    double unitsPerStep = speed / Steps.PerSecond;
                    double stepsSince = unitsPerStep > 0 ? (flight.Distance - segmentStart) / unitsPerStep : 0;
                    float scale = (float)ItemLooks.RedirectorFlightScaleAt(flight.Steps);
                    Place(flight.Renderer, at, (float)ItemLooks.RedirectorFlightAngle(segmentHeading, stepsSince), scale);
                    break;
                }
            }

            return speed <= 0 || flight.Distance >= path.Length;
        }

        private static void Place(SpriteRenderer renderer, Vector2 at, float angle, float scale)
        {
            if (renderer == null)
            {
                return;
            }

            Transform t = renderer.transform;
            t.position = new Vector3(at.x, at.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle); // image_angle and Unity's z both turn anticlockwise on screen
            t.localScale = new Vector3(scale, scale, 1f);
        }

        // Where the flight ends: the teleporter brings the shooter in, the shrink ray bursts on whoever it struck.
        private static void Land(Flight flight)
        {
            Vector2 end = Arena.ToUnity(flight.Path.Impact);
            switch (flight.Item)
            {
                case Item.Teleporter:
                {
                    // teleport_player: the shooter is moved Len = 40 px back along the flight; the rings and the firework
                    // go off where they land.
                    Vector2 landed = flight.Who != null ? flight.Who.Position : end;
                    GmlParticles.Play(GmEffect.Ring, landed, ItemLooks.TeleportArriveRingSize, Color.white);
                    GmlParticles.Play(GmEffect.Ring, landed, ItemLooks.TeleportArriveRingSize, flight.Color);
                    GmlParticles.Play(GmEffect.Firework, landed, ItemLooks.TeleportArriveFireworkSize, Color.white);
                    break;
                }

                case Item.ShrinkRay when flight.Path.End == ShotEndKind.Target:
                    // obj_shrinkray's collision: effect_create_above(ef_firework, x, y, 1, lime).
                    GmlParticles.Play(GmEffect.Firework, end, ItemLooks.ShrinkHitFireworkSize, Steps.Of(ItemLooks.Lime, 1f));
                    break;
            }
        }

        private void ClearAll()
        {
            foreach (Flight flight in _flights)
            {
                Remove(flight);
            }

            _flights.Clear();
        }

        private static void Remove(Flight flight)
        {
            if (flight.Renderer != null)
            {
                Destroy(flight.Renderer.gameObject);
            }
        }

        private sealed class Flight
        {
            public Item Item;
            public ShotPath Path;
            public Player Who;
            public int Seat;
            public Color Color;
            public SpriteRenderer Renderer;
            public float Age;      // seconds
            public float Steps;    // room steps
            public double Distance; // units along the path
        }
    }
}
