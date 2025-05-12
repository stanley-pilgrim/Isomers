using Varwin.Data.ServerData;

namespace Varwin
{
    public static class AuthorAttribution
    {
        public static AuthorAttributionText AuthorLabel;
        
        private static Attribution _attribution;

        public static Attribution ProjectAttribution
        {
            get => _attribution;
            set
            {
                _attribution = value;
                SetAttributionTexts();
            }
        }

        public static Attribution ParseProjectStructure(ProjectStructure structure)
        {
            if (structure.Author != null)
            {
                structure.Author.Name = structure.Author.Name.Trim();
                structure.Author.Company = structure.Author.Company.Trim();
                structure.Author.Url = structure.Author.Url.Trim();
            }

            if (structure.Author != null
                && string.IsNullOrEmpty(structure.Author.Name)
                && string.IsNullOrEmpty(structure.Author.Company))
            {
                structure.Author.Name = "Anonymous";
            }

            structure.ProjectName = structure.ProjectName.Trim();

            if (string.IsNullOrEmpty(structure.ProjectName))
            {
                structure.ProjectName = "Project";
            }

            return new Attribution()
            {
                AuthorName = structure.Author?.Name,
                CompanyName = structure.Author?.Company,
                Url = structure.Author?.Url,
                LicenseCode = $"{structure.License?.Code.ToUpper()} {structure.License?.Version}",
                ProjectName = structure.ProjectName,
            };
        }

        private static void SetAttributionTexts()
        {
            if (!AuthorLabel)
            {
                return;
            }

            AuthorLabel.gameObject.SetActive(true);
            AuthorLabel.UpdateText(_attribution);
        }

        public class Attribution
        {
            public string ProjectName;
            public string AuthorName;
            public string CompanyName;
            public string LicenseCode;
            public string Url;
        }
    }
}
