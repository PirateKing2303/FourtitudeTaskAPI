using System.Text;

namespace FourtitudeTaskAPI
{
    public class Partner
    {

        public readonly string PartnerNo;
        public readonly string PartnerKey;
        public readonly string PartnerPassword;

        public Partner(string PartnerNo, string PartnerKey, string PartnerPassword, bool encryptPassword = true)
        {
            this.PartnerNo = PartnerNo;
            this.PartnerKey = PartnerKey;
            this.PartnerPassword = encryptPassword ? Convert.ToBase64String(Encoding.UTF8.GetBytes(PartnerPassword)) : PartnerPassword;
        }

        public bool IsEquals(Partner partner)
        {
            if (this.PartnerNo != partner.PartnerNo) return false;
            if (this.PartnerKey != partner.PartnerKey) return false;
            if (this.PartnerPassword != partner.PartnerPassword) return false;

            return true;
        }
    }
}
