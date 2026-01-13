namespace healthapp.Helpers
{
    public static class TcValidator
    {
        public static bool Validate(string? tcKimlikNo)
        {
            if (string.IsNullOrEmpty(tcKimlikNo) || tcKimlikNo.Length != 11)
                return false;

            if (!long.TryParse(tcKimlikNo, out _))
                return false;

            if (tcKimlikNo[0] == '0')
                return false;

            int[] digits = new int[11];
            for (int i = 0; i < 11; i++)
            {
                digits[i] = int.Parse(tcKimlikNo[i].ToString());
            }

            int sumOdd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
            int sumEven = digits[1] + digits[3] + digits[5] + digits[7];

            int tenthDigit = ((sumOdd * 7) - sumEven) % 10;
            if (tenthDigit < 0) tenthDigit += 10;

            int eleventhDigit = (sumOdd + sumEven + digits[9]) % 10;

            if (digits[9] != tenthDigit) return false;
            if (digits[10] != eleventhDigit) return false;

            return true;
        }
    }
}