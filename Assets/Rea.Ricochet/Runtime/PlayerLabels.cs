// What a player shows in the world (UR-T38; it was IMGUI in Player.OnGUI): the name under the square and the small health
// bar under that (in a match). TextMeshPro labels and two sprite squares on a root of their own that follows the player, so
// the shrink ray's smaller square does not shrink its name. Player feeds it values each frame; it holds no game state. The
// pickup text and the damage figures float on their own since U.RICO-T48 (FloatText, obj_float_text).
using TMPro;
using UnityEngine;

namespace Ricochet
{
    internal sealed class PlayerLabels
    {
        private const float BarWidth = 1.1f;
        private const float BarHeight = 0.12f;

        private readonly GameObject _root;
        private readonly TextMeshPro _name;
        private readonly Transform _barBack;
        private readonly SpriteRenderer _barFillRenderer;
        private readonly Transform _barFill;

        // Destroys the labels' root once the player's object is gone without a despawn (a scene unload, a Destroy).
        private sealed class Tether : MonoBehaviour
        {
            public GameObject Owner;

            private void Update()
            {
                if (Owner == null)
                {
                    Destroy(gameObject);
                }
            }
        }

        private PlayerLabels(string owner, GameObject tether)
        {
            _root = new GameObject("Labels: " + owner);
            _root.AddComponent<Tether>().Owner = tether;
            // fnt_score, as draw_scores draws them (U.RICO-T44).
            _name = WorldText.Create(_root.transform, "Name", GmlFonts.Score, 0, Color.white, 0.27f);
            _name.transform.localPosition = new Vector3(0f, -0.78f, 0f);

            var back = new GameObject("Bar");
            back.transform.SetParent(_root.transform, false);
            Shapes.Square(back, 1f, WorldText.Order - 2).color = new Color(0f, 0f, 0f, 0.6f);
            back.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
            back.transform.localPosition = new Vector3(0f, -1.02f, 0f);
            _barBack = back.transform;

            var fill = new GameObject("Health");
            fill.transform.SetParent(_root.transform, false);
            _barFillRenderer = Shapes.Square(fill, 1f, WorldText.Order - 1);
            _barFill = fill.transform;
        }

        /// <summary>Makes a player's labels; they are destroyed with <see cref="Destroy"/>.</summary>
        public static PlayerLabels Create(string owner, GameObject tether) => new PlayerLabels(owner, tether);

        /// <summary>Shows or hides everything (the player's object was disabled or enabled).</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        /// <summary>Tears the labels down (the player despawned).</summary>
        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        /// <summary>The name label's text now (for tests).</summary>
        public string NameText => _name.text;

        /// <summary>True while the small health bar shows.</summary>
        public bool BarShowing => _barBack.gameObject.activeSelf;

        /// <summary>Moves the labels to <paramref name="position"/> and sets what they say.</summary>
        public void Refresh(Vector3 position, string name, bool showBar, float health, Color color, bool inRound)
        {
            _root.transform.position = position;
            WorldText.Set(_name, name);

            if (_barBack.gameObject.activeSelf != showBar)
            {
                _barBack.gameObject.SetActive(showBar);
                _barFill.gameObject.SetActive(showBar);
            }

            if (showBar)
            {
                float fraction = Mathf.Clamp01(health);
                _barFill.localScale = new Vector3(Mathf.Max(0.0001f, (BarWidth - 0.04f) * fraction), BarHeight - 0.04f, 1f);
                _barFill.localPosition = new Vector3(-(BarWidth - 0.04f) / 2f + (BarWidth - 0.04f) * fraction / 2f, -1.02f, 0f);
                Color fill = color;
                fill.a = inRound ? 1f : 0.3f;
                _barFillRenderer.color = fill;
            }
        }
    }
}
