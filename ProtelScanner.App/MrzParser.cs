using System.Linq;

namespace ProtelScanner.App
{
    public static class MrzParser
    {
        public static MrzData Parse(string mrzText)
        {
            // TODO: drop in your real MRZ‐parsing logic here.
            var lines = mrzText
                        .Split('\n')
                        .Select(l => l.Trim())
                        .ToArray();

            return new MrzData
            {
                DocumentType = lines.ElementAtOrDefault(0)?.Substring(0, 1),
                IssuingCountry = lines.ElementAtOrDefault(0)?.Substring(2, 3),
                LastName = "…",
                FirstName = "…",
                DocumentNumber = "…",
                Nationality = "…",
                BirthDate = "…",
                Sex = "…",
                ExpirationDate = "…",
                PersonalNumber = "…"
            };
        }
    }
}
