using System;


namespace BarclayCard_Smartpay
{
    class Program
    {
        static void Main(string[] args)
        {

            string answer = "Y";
            do
            {
                Console.Clear();

                Console.WriteLine("\n\tPaymentSense Payment Simulator");
                Console.WriteLine("\t_______________________________\n");
                int amount = 0;

                Console.Write("\nEnter the Amount: ");

                amount = Convert.ToInt32(Console.ReadLine());

                using (var payment = new BarclayCardSmartpayApi())
                {
                    try
                    {
                        payment.TransactionProcess(amount);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: " + ex.Message);
                        Console.ResetColor();
                    }
                    Console.Write("\n\nWould you like to add another payment? (Y/N): ");
                    answer = Console.ReadLine().ToUpper();
                }
            } while (answer == "Y");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

}
