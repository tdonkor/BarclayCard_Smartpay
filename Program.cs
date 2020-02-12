using System;
using System.IO;
using System.Reflection;

namespace BarclayCard_Smartpay
{
    class Program
    {

        private static readonly string ticketPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ticket");
        private static string transactionRef = string.Empty;

        static void Main(string[] args)
        {
            Random rand = new Random();
            int randNum = rand.Next(1, 1000000);

            transactionRef = randNum.ToString();

            string answer = "Y";
            do
            {
                Console.Clear();

                Console.WriteLine("\n\tBarclaycard Smartpay Payment Simulator");
                Console.WriteLine("\t_______________________________________\n");
                int amount = 0;

                Console.Write("\nEnter the Amount: ");

                amount = Convert.ToInt32(Console.ReadLine());

                using (var payment = new BarclayCardSmartpayApi())
                {
                    try
                    {
                        var payResult = payment.Pay(amount, transactionRef, out TransactionReceipts payReceipts);
                        Console.WriteLine($"Pay Result: {payResult}");

                        if (payResult != DiagnosticErrMsg.OK)
                        {
                          
                            Console.WriteLine(payReceipts.CustomerReturnedReceipt.ToString());
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n16) ********* Successful Response from Pay ********\n");

                            //persist the Merchant transaction
                            PersistTransaction(payReceipts.MerchantReturnedReceipt, "MERCHANT");

                            CreateTicket(payReceipts.CustomerReturnedReceipt, "CUSTOMER");
                            Console.ResetColor();
                        }
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

        /// <summary>
        /// Persist the transaction as Text file
        /// with Customer and Merchant receiept
        /// </summary>
        /// <param name="result"></param>
        private static void PersistTransaction(string receipt, string ticketType)
        {
            try
            {

                var outputDirectory = Path.GetFullPath(@"C:\Customer Payment Drivers\PaymentTestCodewithout ATP\BarclayCard_Smartpay_Connect\BarclayCard_Smartpay\");
                var outputPath = Path.Combine(outputDirectory, $"{DateTime.Now:yyyyMMddHHmmss}_{ticketType}_ticket.txt");

                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                //Console.WriteLine($"Persisting {ticketType} to {outputPath}");

                //Write the new ticket
                File.WriteAllText(outputPath, receipt.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Persist Transaction exception.");
                Console.WriteLine(ex);
            }
        }

        private static void CreateTicket(string ticket, string ticketType)
        {
            try
            {
                Console.WriteLine($"Persisting {ticketType} to {ticketPath}");

                //Write the new ticket
                File.WriteAllText(ticketPath, ticket);

                //persist the transaction
                PersistTransaction(ticket, ticketType);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {ticketType} persisting ticket.");
                Console.WriteLine(ex);
            }
        }
    }

}
