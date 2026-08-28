namespace LicenseManager.Core.Models
{
    public class License
    {
        public Guid Id { get; set; }

        public Guid CompanyId { get; set; }

        public Guid ProjectId { get; set; }

        public string LicenseKey { get; set; } =
            string.Empty;

        public List<string> Modules { get; set; } =
            new();


        // -------------------------------------------------
        // Son imzalı lisans dosyasının üretildiği tarih
        // -------------------------------------------------
        public DateTime IssuedAt { get; set; }


        // -------------------------------------------------
        // Client'a verilen mevcut imzalı lisans dosyasının
        // son geçerlilik tarihi.
        //
        // Normalde:
        // IssuedAt + 30 gün
        //
        // Ancak ValidUntil bundan önceyse
        // ExpiryDate = ValidUntil olur.
        // -------------------------------------------------
        public DateTime ExpiryDate { get; set; }


        // -------------------------------------------------
        // Müşterinin GERÇEK lisans hakkının son tarihi.
        //
        // Renewal veya Key Rotation bu tarihi
        // kesinlikle uzatamaz.
        // -------------------------------------------------
        public DateTime ValidUntil { get; set; }


        public bool IsBlocked { get; set; }

        public DateTime CreatedAt { get; set; }


        public Company Company { get; set; } =
            null!;

        public Project Project { get; set; } =
            null!;
    }
}