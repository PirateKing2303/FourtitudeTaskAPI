using log4net;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FourtitudeTaskAPI.Controllers
{
    [ApiController]
    [Route("api/submittrxmessage")]
    public class SubmitTrxMessageController : ControllerBase
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(SubmitTrxMessageController));
        private const string iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
        private const string sigTimestampFormat = "yyyyMMddHHmmss";

        // Initialize Allowed Partners
        private readonly HashSet<Partner> _allowedPartners = [new Partner("FG-00001", "FAKEGOOGLE", "FAKEPASSWORD1234"), new Partner("FG-00002", "FAKEPEOPLE", "FAKEPASSWORD4578")];

        public SubmitTrxMessageController()
        {
        }

        [HttpPost]
        public IActionResult SubmitTrxMessage([FromBody] SubmitTrxMessageRequest request)
        {
            string transactionId = Guid.NewGuid().ToString();
            _logger.Info($"Start submit transaction: {transactionId}");


            // 1. Request validation
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var validateRequest = ValidateRequest(request);

            if (!string.IsNullOrWhiteSpace(validateRequest))
                return BadRequest(JsonSerializer.Serialize(new SubmitTrxMessageErrorResponse(validateRequest)));


            // 2. Calculation
            long totalAmount = request.TotalAmount;
            decimal discountPercentage = CalculateDiscounts(totalAmount);
            long totalDiscount = (long)(totalAmount * discountPercentage);
            long finalAmount = totalAmount - totalDiscount;

            var response = JsonSerializer.Serialize(new SubmitTrxMessageResponse
            {
                TotalAmount = totalAmount,
                TotalDiscount = totalDiscount,
                FinalAmount = finalAmount
            });


            _logger.Info($"End submit transaction: {transactionId} [{response}]");

            return Ok(response);
        }



        // Request validation methods
        private string ValidateRequest(SubmitTrxMessageRequest request)
        {
            _logger.Debug($"Validating request: {request}");

            if (!IsBase64String(request.PartnerPassword))
                return "PartnerPassword must be a valid Base64 encoded string.";

            if (!IsValidIso8601UtcStrict(request.Timestamp))
                return "Timestamp must be a valid ISO 8601 UTC datetime string (e.g., 2024-08-15T02:11:22.0000000Z).";

            if (!IsAuthorized(request))
                return "Access Denied!";

            if (!IsValidTotalAmount(request.TotalAmount, request.Items))
                return "Invalid Total Amount.";

            if (IsExpired(request.Timestamp))
                return "Expired.";

            return string.Empty;
        }

        private static bool IsBase64String(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return false;

            Span<byte> buffer = new Span<byte>(new byte[base64.Length]);
            return Convert.TryFromBase64String(base64, buffer, out _);
        }

        private static bool IsValidIso8601UtcStrict(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
                return false;

            return DateTime.TryParseExact(
                timestamp,
                iso8601Format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out _);
        }

        private bool IsAuthorized(SubmitTrxMessageRequest request)
        {
            _logger.Debug("Validate authorization.");

            try
            {
                // Validate Allowed Partners
                Partner requestPartner = new Partner(request.PartnerRefNo, request.PartnerKey, request.PartnerPassword, false);
                //if (!_allowedPartners.Contains(requestPartner))
                //    return false;

                bool isExistingPartner = false;
                foreach (var partner in _allowedPartners)
                {
                    if (requestPartner.IsEquals(partner))
                    {
                        isExistingPartner = true;
                        break;
                    }
                }

                if (!isExistingPartner)
                    return false;

                // Map keys except timestamp, sig, password, items
                List<string> keyMap = typeof(SubmitTrxMessageRequest)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(prop => prop.Name)
                    .Where(k => k != "Sig" && k != "Timestamp" && k != "PartnerPassword" && k != "Items")
                    .OrderBy(k => k)
                    .ToList();

                // Map values for each key
                List<string?> valueMap = keyMap.Select(key => request.GetType().GetProperty(key)?.GetValue(request)?.ToString()).ToList();

                // Get sigTimestamp
                string sigTimestamp = DateTime.ParseExact(
                    request.Timestamp,
                    iso8601Format,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal
                    ).ToString(sigTimestampFormat);

                // Build Signature
                string sigString = BuildSignature(sigTimestamp, valueMap, request.PartnerPassword);

                // Generate Final Signature
                string finalSigString = GenerateSignature(sigString);


                return finalSigString.Equals(request.Sig);
            } catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private bool IsValidTotalAmount(long totalAmount, List<ItemDetail> itemList)
        {
            _logger.Debug("Validate Total Amount.");

            if (itemList.Count != 0)
            {
                long itemsAmount = 0;

                foreach (ItemDetail item in itemList)
                {
                    itemsAmount += item.Qty * item.UnitPrice;
                }

                return itemsAmount.Equals(totalAmount);
            }

            return false;
        }

        private bool IsExpired(string timestamp)
        {
            _logger.Debug("Validate Expiry.");

            var serverTime = DateTime.UtcNow;
            var requestTime = DateTime.ParseExact(
                timestamp,
                iso8601Format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal
            );

            var timeMargin = 5; // Set to 5-minute margin
            var timeDifference = (serverTime - requestTime).Duration();

            return timeDifference > TimeSpan.FromMinutes(timeMargin);
        }

        private string BuildSignature(string sigTimestamp, List<string?> valueMap, string partnerPassword)
        {
            _logger.Debug("Build Signature string.");

            // Concatenate values (sigTimestamp + requestValues + partnerPassword)
            StringBuilder sb = new();
            valueMap.ForEach(k => sb.Append(k));
            string requestValues = sb.ToString();

            return sigTimestamp + requestValues + partnerPassword;
        }

        private string GenerateSignature(string sigString)
        {
            _logger.Debug("Generate Signature string.");

            // Apply SHA-256 hashing (UTF-8 input)(lowercase hexadecimal output)
            byte[] hashArray = SHA256.HashData(Encoding.UTF8.GetBytes(sigString));
            StringBuilder sb = new(hashArray.Length * 2);
            foreach (var h in hashArray)
            {
                sb.AppendFormat("{0:x2}", h);
            }
            string hashOutput = sb.ToString();

            // Convert to Base64
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(hashOutput));
        }



        // Request calculation methods
        private decimal CalculateDiscounts(long totalAmount)
        {
            _logger.Debug($"Calculate Discounts based on Total Amount: {totalAmount}");

            // Base Discount
            decimal baseDiscount = CalculateBaseDiscount(totalAmount);

            // Conditional Discount
            decimal conditionalDiscount = CalculateConditionalDiscount(totalAmount);

            // Cap on Max Discount
            const decimal maxDiscount = 0.2m;
            decimal finalDiscount = Math.Min(baseDiscount + conditionalDiscount, maxDiscount);

            return finalDiscount;
        }

        private decimal CalculateBaseDiscount(long totalAmount)
        {
            _logger.Debug("Calculate Base Discount.");

            if (totalAmount < 20000) return 0m; // less than 200.00 MYR (amount in cents)
            if (totalAmount <= 50000) return 0.05m;
            if (totalAmount <= 80000) return 0.07m;
            if (totalAmount <= 120000) return 0.10m;
            return 0.15m;
        }

        private decimal CalculateConditionalDiscount(long totalAmount)
        {
            _logger.Debug("Calculate Conditional Discount.");

            long amount = (long)totalAmount / 100; // convert cents to MYR

            // Prime Number Discount
            if (totalAmount > 50000 && IsPrime(amount))
            {
                return 0.08m;
            }

            // Ends With Five Discount
            if (totalAmount > 90000 && amount % 10 == 5)
            {
                return 0.10m;
            }

            return 0m;
        }

        private static bool IsPrime(long number)
        {
            if (number < 2) return false;
            if (number == 2) return true;
            if (number % 2 == 0) return false;

            var boundary = (long)Math.Floor(Math.Sqrt(number));

            for (long i = 3; i <= boundary; i += 2)
                if (number % i == 0)
                    return false;

            return true;
        }
    }
}
