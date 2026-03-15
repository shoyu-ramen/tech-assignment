using UnityEngine;
using UnityEngine.UI;

namespace HijackPoker.UI
{
    /// <summary>
    /// Renders the dark-themed poker table: background fill, oval felt surface
    /// with a subtle inner glow and border.
    /// </summary>
    public class TableView : MonoBehaviour
    {
        private RectTransform _surface;

        public RectTransform Surface => _surface;

        public static TableView Create(Transform parent)
        {
            // Background — fills entire canvas
            var bgRt = UIFactory.CreatePanel("Background", parent, UIFactory.Background);
            UIFactory.StretchFill(bgRt);

            // Subtle radial gradient hint (large dark-to-slightly-lighter panel behind table)
            var gradientRt = UIFactory.CreatePanel("GradientHint", bgRt,
                UIFactory.GradientHint);
            gradientRt.anchorMin = LayoutConfig.GradientMin;
            gradientRt.anchorMax = LayoutConfig.GradientMax;
            gradientRt.offsetMin = Vector2.zero;
            gradientRt.offsetMax = Vector2.zero;

            // Table container
            var tableGo = new GameObject("TableView", typeof(RectTransform));
            tableGo.transform.SetParent(bgRt, false);
            var tableRt = tableGo.GetComponent<RectTransform>();
            UIFactory.StretchFill(tableRt);

            var view = tableGo.AddComponent<TableView>();

            // Outer glow — slightly larger, very faint cyan
            var glowRt = UIFactory.CreatePanel("TableGlow", tableRt);
            glowRt.anchorMin = LayoutConfig.TableGlowMin;
            glowRt.anchorMax = LayoutConfig.TableGlowMax;
            glowRt.offsetMin = Vector2.zero;
            glowRt.offsetMax = Vector2.zero;
            var tableRoundedSprite = TextureGenerator.GetRoundedRect(128, 64, 24);

            var glowImg = glowRt.gameObject.AddComponent<Image>();
            glowImg.color = UIFactory.TableGlowColor;
            glowImg.sprite = tableRoundedSprite;
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;

            // Felt surface — responsive to portrait/landscape
            var surfaceRt = UIFactory.CreatePanel("Surface", tableRt);
            surfaceRt.anchorMin = LayoutConfig.TableSurfaceMin;
            surfaceRt.anchorMax = LayoutConfig.TableSurfaceMax;
            surfaceRt.offsetMin = Vector2.zero;
            surfaceRt.offsetMax = Vector2.zero;

            // Table border
            var borderImg = surfaceRt.gameObject.AddComponent<Image>();
            borderImg.color = UIFactory.TableBorderColor;
            borderImg.sprite = tableRoundedSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.raycastTarget = false;

            // Inner felt with inset for border effect
            var feltRt = UIFactory.CreatePanel("Felt", surfaceRt, UIFactory.FeltColor);
            var feltImg = feltRt.GetComponent<Image>();
            if (feltImg == null) feltImg = feltRt.gameObject.AddComponent<Image>();
            feltImg.sprite = tableRoundedSprite;
            feltImg.type = Image.Type.Sliced;
            feltRt.anchorMin = Vector2.zero;
            feltRt.anchorMax = Vector2.one;
            feltRt.offsetMin = new Vector2(3, 3);
            feltRt.offsetMax = new Vector2(-3, -3);

            // Inner highlight — very subtle lighter center area
            var innerHighlight = UIFactory.CreatePanel("InnerHighlight", feltRt,
                UIFactory.FeltHighlight);
            innerHighlight.anchorMin = new Vector2(0.1f, 0.1f);
            innerHighlight.anchorMax = new Vector2(0.9f, 0.9f);
            innerHighlight.offsetMin = Vector2.zero;
            innerHighlight.offsetMax = Vector2.zero;

            view._surface = tableRt;
            return view;
        }
    }
}
