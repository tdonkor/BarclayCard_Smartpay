using System;


namespace BarclayCard_Smartpay
{
    public class Utils
    {
        /// <summary>
        /// Check the numeric value of the amount
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static int GetNumericAmountValue(int amount)
        {

            if (amount <= 0)
            {
                Console.WriteLine("Invalid pay amount");
                amount = 0;
            }

            return amount;
        }
    }
}
