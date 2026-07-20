using UnityEngine;

namespace DuloGames.UI
{
    public class ColorScheme : ScriptableObject
    {
        [Header("Image Colors")]
        [SerializeField] private Color m_ImagePrimary = Color.white;
        [SerializeField] private Color m_ImageSecondary = Color.white;
        [SerializeField] private Color m_ImageLight = Color.white;
        [SerializeField] private Color m_ImageDark = Color.white;
        [SerializeField] private Color m_ImageEffect = Color.white;
        [SerializeField] private Color m_ImageEffect2 = Color.white;
        [SerializeField] private Color m_ImageEffect3 = Color.white;
        [SerializeField] private Color m_ImageBordersPrimary = Color.white;
        [HideInInspector][SerializeField] private Color m_ImageBordersSecondary = Color.white;

        [Header("Button Colors")]
        [SerializeField] private Color m_ButtonForeground = Color.white;
        [SerializeField] private Color m_ButtonEffect = Color.white;

        [Header("Window Colors")]
        [SerializeField] private Color m_WindowHeader = Color.white;

        [Header("Animation Colors")]
        [SerializeField] private Color m_Animation = Color.white;
        
        /// <summary>
        /// Gets or sets the Primary image color shade.
        /// </summary>
        public Color imagePrimary
        {
            get { return this.m_ImagePrimary; }
            set { this.m_ImagePrimary = value; }
        }

        /// <summary>
        /// Gets or sets the Secondary image color shade.
        /// </summary>
        public Color imageSecondary
        {
            get { return this.m_ImageSecondary; }
            set { this.m_ImageSecondary = value; }
        }

        /// <summary>
        /// Gets or sets the light image color shade.
        /// </summary>
        public Color imageLight
        {
            get { return this.m_ImageLight; }
            set { this.m_ImageLight = value; }
        }

        /// <summary>
        /// Gets or sets the dark image color shade.
        /// </summary>
        public Color imageDark
        {
            get { return this.m_ImageDark; }
            set { this.m_ImageDark = value; }
        }
        
        /// <summary>
        /// Gets or sets the Effect image color shade.
        /// </summary>
        public Color imageEffect
        {
            get { return this.m_ImageEffect; }
            set { this.m_ImageEffect = value; }
        }

        /// <summary>
        /// Gets or sets the Effect 2 image color shade.
        /// </summary>
        public Color imageEffect2
        {
            get { return this.m_ImageEffect2; }
            set { this.m_ImageEffect2 = value; }
        }

        /// <summary>
        /// Gets or sets the Effect 3 image color shade.
        /// </summary>
        public Color imageEffect3
        {
            get { return this.m_ImageEffect3; }
            set { this.m_ImageEffect3 = value; }
        }

        /// <summary>
        /// Gets or sets the Primary Borders image color shade.
        /// </summary>
        public Color imageBordersPrimary
        {
            get { return this.m_ImageBordersPrimary; }
            set { this.m_ImageBordersPrimary = value; }
        }

        /// <summary>
        /// Gets or sets the Secondary Borders image color shade.
        /// </summary>
        public Color imageBordersSecondary
        {
            get { return this.m_ImageBordersSecondary; }
            set { this.m_ImageBordersSecondary = value; }
        }

        /// <summary>
        /// Gets or sets the button foreground color.
        /// </summary>
        public Color buttonForeground
        {
            get { return this.m_ButtonForeground; }
            set { this.m_ButtonForeground = value; }
        }

        /// <summary>
        /// Gets or sets the button effect color.
        /// </summary>
        public Color buttonEffect
        {
            get { return this.m_ButtonEffect; }
            set { this.m_ButtonEffect = value; }
        }

        /// <summary>
        /// Gets or sets the window header color.
        /// </summary>
        public Color windowHeader
        {
            get { return this.m_WindowHeader; }
            set { this.m_WindowHeader = value; }
        }
        
        /// <summary>
        /// Gets or sets the animation color.
        /// </summary>
        public Color animation
        {
            get { return this.m_Animation; }
            set { this.m_Animation = value; }
        }

        /// <summary>
        /// Applies the color scheme.
        /// </summary>
        public void ApplyColorScheme()
        {
            // Get all the color scheme elements in the scene
            ColorSchemeElement[] elements = FindObjectsByType<ColorSchemeElement>(FindObjectsSortMode.None);

            foreach (ColorSchemeElement element in elements)
            {
                this.ApplyToElement(element);
            }

            ColorSchemeElement_SelectField[] selectElements = FindObjectsByType<ColorSchemeElement_SelectField>(FindObjectsSortMode.None);

            foreach (ColorSchemeElement_SelectField element in selectElements)
            {
                this.ApplyToElement(element);
            }

            // Set the color scheme as active
            if (ColorSchemeManager.Instance != null)
                ColorSchemeManager.Instance.activeColorScheme = this;
        }
        
        /// <summary>
        /// Gets a color shade.
        /// </summary>
        /// <param name="shade">The shade.</param>
        /// <returns>The color.</returns>
        public Color GetColorShade(ColorSchemeShade shade)
        {
            Color newColor = Color.white;

            switch (shade)
            {
                case ColorSchemeShade.Primary:
                    newColor = this.m_ImagePrimary;
                    break;
                case ColorSchemeShade.Secondary:
                    newColor = this.m_ImageSecondary;
                    break;
                case ColorSchemeShade.Light:
                    newColor = this.m_ImageLight;
                    break;
                case ColorSchemeShade.Dark:
                    newColor = this.m_ImageDark;
                    break;
                case ColorSchemeShade.Effect:
                    newColor = this.m_ImageEffect;
                    break;
                case ColorSchemeShade.Effect2:
                    newColor = this.m_ImageEffect2;
                    break;
                case ColorSchemeShade.Effect3:
                    newColor = this.m_ImageEffect3;
                    break;
                case ColorSchemeShade.BordersPrimary:
                    newColor = this.m_ImageBordersPrimary;
                    break;
                case ColorSchemeShade.BordersSecondary:
                    newColor = this.m_ImageBordersSecondary;
                    break;
                case ColorSchemeShade.ButtonPrimary:
                    newColor = this.m_ButtonForeground;
                    break;
                case ColorSchemeShade.ButtonSecondary:
                    newColor = this.m_ButtonEffect;
                    break;
                case ColorSchemeShade.WindowHeader:
                    newColor = this.m_WindowHeader;
                    break;
                case ColorSchemeShade.Animation:
                    newColor = this.m_Animation;
                    break;
            }

            return newColor;
        }


        /// <summary>
        /// Applies the color scheme to the specified element.
        /// </summary>
        /// <param name="element"></param>
        public void ApplyToElement(IColorSchemeElement element)
        {
            if (element == null)
                return;
            
            // Get the color
            Color newColor = this.GetColorShade(element.shade);

            // Apply
            element.Apply(newColor);
        }
    }
}
