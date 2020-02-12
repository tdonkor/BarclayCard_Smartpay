using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Data.SqlClient;

namespace BarclayCard_Smartpay
{


    public class BarclayCardSmartpayApi : IDisposable
    {

        static int number = 0;
        string transNum = "000000";
        string transactionLimit = "999999";
        string toBeSearched = "reference=";
        string reference = string.Empty;
        string description = string.Empty;

        
        IPHostEntry ipHostInfo;
        IPAddress ipAddress;
        IPEndPoint remoteEP;

        // payment success flag
        DiagnosticErrMsg isSuccessful;

        // payment response flag
        string paymentSuccessful = string.Empty;

        //bool Authorisation successful flag
        bool authorisationFlag = false;
        byte authBit = 0;

        // Data buffer for incoming data.
        byte[] bytes = new byte[4096];

        int port;
        int currency;
        int country;
        string sourceId;
        string connectionString;
        string tableName;

        float tax = 0.0f;
        float exTax = 0.0f;
        float incTax = 0.0f;
        float total = 0.0f;

        //operation XML sent to Smartpay via a socket
        SmartPayOperations smartpayOps;

        public BarclayCardSmartpayApi()
        {
            currency = 826;
            country = 826;
            port = 8000;
            sourceId = "DK01.P001";
            connectionString = "Data Source = 192.168.254.75,49170; Initial Catalog = TestDB; Persist Security Info = True; User ID = sa; Password = acrelec";
            tableName = "PaymentDetails";


            // Establish the remote endpoint for the socket.  
            // This example uses port 8000 on the local computer.  
            ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
             ipAddress = ipHostInfo.AddressList[0];
             remoteEP = new IPEndPoint(ipAddress, port);
            smartpayOps = new SmartPayOperations();
        }

        public DiagnosticErrMsg Pay(int amount, string transactionRef, out TransactionReceipts transactionReceipts)
        {

            XDocument paymentXml = null;
            XDocument procTranXML = null;
            XDocument customerSuccessXML = null;
            XDocument processTransRespSuccessXML = null;
            XDocument finaliseXml = null;
            XDocument voidXml = null;
            XDocument finaliseSettleXml = null;
            XDocument paymentSettlementXml = null;
            XDocument procSettleTranXML = null;


            int intAmount;

            //set return flag true
            isSuccessful = DiagnosticErrMsg.OK;

            // check for a success or failure string from smartpay
            string submitPaymentResult = string.Empty;
            string finaliseResult = string.Empty;
            string finaliseSettleResult = string.Empty;
            string submitSettlePaymentResult = string.Empty;
            string description = transactionRef;

            number++;
            transNum = number.ToString().PadLeft(6, '0');

            //check for transNum max value
            if (transNum == transactionLimit)
            {
                //reset
                number = 1;
                transNum = number.ToString().PadLeft(6, '0');
            }

            transactionReceipts = new TransactionReceipts();

            //check amount is valid
            intAmount = Utils.GetNumericAmountValue(amount);

            if (intAmount == 0)
            {
    
                throw new Exception("Error in Amount value...");
            }

            Console.WriteLine("\n1) Amount is ***** " + amount/100 + " *****\n");
            Console.WriteLine("2) Transaction Number is ***** " + transNum + " *****\n");
            //Console.WriteLine("Transaction ref is ***** " + reference + " *****\n\n");

            /*********************** AUTHORISATION SECTION ***************************
            *                                                                       
            * Submittal – Submitting data to Smartpay Connect ready for processing. 
            * SUBMIT PAYMENT Process           
            * 
            *************************************************************************/

            //process Payment XML
            paymentXml = smartpayOps.Payment(amount, transNum, description, sourceId, currency, country);

            Socket paymentSocket = CreateSocket();
            //Console.WriteLine("paymentSocket Open: " + SocketConnected(paymentSocket));

            //send submitpayment to smartpay - check response
            string paymentResponseStr = sendToSmartPay(paymentSocket, paymentXml, "SUBMITPAYMENT");

            //check response from Smartpay is not Null or Empty
            if (CheckIsNullOrEmpty(paymentResponseStr, "Submit Authorisation Payment")) isSuccessful = DiagnosticErrMsg.NOTOK;
            else
            {
                //check response outcome
                submitPaymentResult = CheckResult(paymentResponseStr);

                if (submitPaymentResult.ToLower() == "success")
                {
                    Console.WriteLine("3) Successful payment submitted\n");
                }
                else
                {
                    Console.WriteLine("Payment failed");
                    isSuccessful = DiagnosticErrMsg.NOTOK;
                }
            }

            //checkSocket closed
            //Console.WriteLine("paymentSocket Open: " + SocketConnected(paymentSocket));


            /************************************************************************************
           *                                                                                   *
           * Transactional – Processing of a transaction submitted during the submittal phase. *
           * PROCESSTRANSACTION process - gets the Merchant receipt                          *
           *                                                                                   *
           *************************************************************************************/

            //create processtransaction socket
            Socket processSocket = CreateSocket();

            //Console.WriteLine("ProcessTransaction Socket Open: " + SocketConnected(processSocket));

            //Process Transaction XML
            procTranXML = smartpayOps.ProcessTransaction(transNum);

            //send processTransaction - check response
            string processTranReturnStr = sendToSmartPay(processSocket, procTranXML, "PROCESSTRANSACTION");

            //check response from Smartpay is not NULL or Empty
            if (CheckIsNullOrEmpty(processTranReturnStr, "Process Transaction")) isSuccessful = DiagnosticErrMsg.NOTOK;
            else
            {
                //check that the response contains a Receipt or is not NULL this is the Merchant receipt
                transactionReceipts.MerchantReturnedReceipt = ExtractXMLReceiptDetails(processTranReturnStr);

                //Check the merchant receipt is populated
                if (CheckIsNullOrEmpty(transactionReceipts.MerchantReturnedReceipt, "Merchant Receipt populated")) isSuccessful = DiagnosticErrMsg.NOTOK;
                else
                {
                    //check if reciept has a successful transaction
                    if (transactionReceipts.MerchantReturnedReceipt.Contains("DECLINED"))
                    {
                        Console.WriteLine("Merchant Receipt has Declined Transaction.");
                        isSuccessful = DiagnosticErrMsg.NOTOK;
                    }
                }
            }

            //check socket closed
            //Console.WriteLine("ProcessTransaction Socket Open: " + SocketConnected(processSocket));

            /******************************************************************************
            *                                                                             *
            * Interaction – Specific functionality for controlling POS and PED behaviour. *
            * gets the Customer receipt                                                   *
            *                                                                      *
            *******************************************************************************/

            //create customer socket
            Socket customerSuccessSocket = CreateSocket();

            //Console.WriteLine("customerSuccess Socket Open: " + SocketConnected(customerSuccessSocket));

            //process customerSuccess XML
            customerSuccessXML = smartpayOps.PrintReciptResponse(transNum);

            string customerResultStr = sendToSmartPay(customerSuccessSocket, customerSuccessXML, "CUSTOMERECEIPT");

            //Check response from Smartpay is not Null or Empty
            if (CheckIsNullOrEmpty(customerResultStr, "Customer Receipt process")) isSuccessful = DiagnosticErrMsg.NOTOK;
            else
            {
                transactionReceipts.CustomerReturnedReceipt = ExtractXMLReceiptDetails(customerResultStr);

                //check returned receipt is not Null or Empty
                if (CheckIsNullOrEmpty(transactionReceipts.CustomerReturnedReceipt, "Customer Receipt returned")) isSuccessful = DiagnosticErrMsg.NOTOK;
                else
                {
                    //check if reciept has a successful transaction
                    if (transactionReceipts.CustomerReturnedReceipt.Contains("DECLINED"))
                    {
                        Console.WriteLine("Customer Receipt has Declined Transaction.");
                        isSuccessful = DiagnosticErrMsg.NOTOK;
                    }
                }
            }

            //Console.WriteLine("customerSuccess Socket Open: " + SocketConnected(customerSuccessSocket));

            /***********************************************************************************************************
           *                                                                                                           
           * Interaction – Specific functionality for controlling PoS and PED behaviour. ( ProcessTransactionResponse)  
           * PROCESSTRANSACTIONRESPONSE                                                                                            
           *************************************************************************************************************/

            Socket processTransactionRespSocket = CreateSocket();

           // Console.WriteLine("processTransactionRespSocket Socket Open: " + SocketConnected(processTransactionRespSocket));
            processTransRespSuccessXML = smartpayOps.PrintReciptResponse(transNum);

            string processTransRespStr = sendToSmartPay(processTransactionRespSocket, processTransRespSuccessXML, "PROCESSTRANSACTIONRESPONSE");

            //check response from Smartpay is not Null or Empty
            if (CheckIsNullOrEmpty(processTransRespStr, "Process Transaction Response")) isSuccessful = DiagnosticErrMsg.NOTOK;
            else
            {
                //get the reference value from the process Transaction Response
                // this is needed for the settlement process
                //
                reference = GetReferenceValue(processTransRespStr);

                //Console.WriteLine($"REFERENCE Number = {reference}");

                if (processTransRespStr.Contains("declined"))
                {
                    Console.WriteLine("***** Auth Process Transaction Response has Declined Transaction. *****");
                    isSuccessful = DiagnosticErrMsg.NOTOK;
                }
            }

           // Console.WriteLine("processTransRespSuccessXML Socket Open: " + SocketConnected(processTransactionRespSocket));

            /*****************************************************************************************************************
             *                                                                                                               
             * finalise Response message so that the transaction can be finalised and removed from Smartpay Connect's memory 
             *   
             *   FINALISE   
             *   
             ******************************************************************************************************************/

            Socket finaliseSocket = CreateSocket();
            //Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));

            finaliseXml = smartpayOps.Finalise(transNum);

            string finaliseStr = sendToSmartPay(finaliseSocket, finaliseXml, "FINALISE");

            //check response from Smartpay is not Null or Empty
            if (CheckIsNullOrEmpty(finaliseStr, "Finalise Authorisation")) isSuccessful = DiagnosticErrMsg.NOTOK;
            else
            {
                finaliseResult = CheckResult(finaliseStr);

                if (finaliseResult == "success")
                {
                    Console.WriteLine("4) ****** Authorisation Transaction Finalised Successfully******\n");
                }
                else
                {
                    Console.WriteLine("***** Authorisation Transaction not Finalised *****");
                    isSuccessful = DiagnosticErrMsg.NOTOK;
                }
            }

            //Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));

         /*****************************************************************************************************************
          *                                                                                                               
          * check if the Authorisation has been successful    
          * 
          ******************************************************************************************************************/


            if (isSuccessful == DiagnosticErrMsg.OK)
            {
                authorisationFlag = true;
                authBit = 1;
                Console.WriteLine("5) Authorisation is True\n");
            }
            else
            {
                authorisationFlag = false;
                authBit = 0;
               
            }

            //TODO get VAT values in this case calculate them use 20% in this example
            //will get these values from the database so this will be removed

            float vatRate = 0.2f;
            tax = (amount * vatRate) / 100.0f;

            total = amount / 100.0f;
            incTax = amount / 100.0f;
            exTax = (total - tax);

            /******************************************************************************************
             * 
             * generate an order number for if the transaction is valid to use(Remove from final code)
             * 
             * ****************************************************************************************/

            Random rand = new Random();
            int randNum = rand.Next(0, 10);

           /* ***********************Open Database***************************
           *
           * Write the transaction details and 
           * Authorisation check to the database.
           * 
           * if Authorisation check successful carry on else 
           * end Authorisation and close database connection
           * 
           *  TODO - send the tax details to the database 
           *  (these will be retrieviedin the final code)
           *  
           ******************************************************************/
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = connectionString;
                Console.WriteLine($"6) Open database with Connection String: {connectionString} to update the Authorisation response\n");

                conn.Open();
                Console.WriteLine($"7) Connect to the Database Table: {tableName}\n");
                //
                // overwrite the transactionNumber and Authorisation to the database.
                // create and configure a new command - will remove the Tax in the final code
                SqlCommand comm = new SqlCommand($"UPDATE {tableName} SET AuthCheck = @Authcheck , TransNum = @TransNum, TaxTotal = @TaxTotal, IncTax = @IncTax, ExTax = @ExTax, OrderNum = @OrderNum", conn);

                // define parameters used in command object
                SqlParameter p1 = comm.CreateParameter();
                p1.ParameterName = "@AuthCheck";
                p1.SqlDbType = System.Data.SqlDbType.Bit;
                p1.Value = authBit;
                comm.Parameters.Add(p1);

                SqlParameter p2 = comm.CreateParameter();
                p2.ParameterName = "@TransNum";
                p2.SqlDbType = System.Data.SqlDbType.VarChar;
                p2.Value = transNum;
                comm.Parameters.Add(p2);

                //TODO Tax will be removed in final code
                SqlParameter p3 = comm.CreateParameter();
                p3.ParameterName = "@TaxTotal";
                p3.SqlDbType = System.Data.SqlDbType.Float;
                p3.Value = tax;
                comm.Parameters.Add(p3);

                SqlParameter p4 = comm.CreateParameter();
                p4.ParameterName = "@IncTax";
                p4.SqlDbType = System.Data.SqlDbType.Float;
                p4.Value = incTax;
                comm.Parameters.Add(p4);

                SqlParameter p5 = comm.CreateParameter();
                p5.ParameterName = "@ExTax";
                p5.SqlDbType = System.Data.SqlDbType.Float;
                p5.Value = exTax;
                comm.Parameters.Add(p5);


                SqlParameter p6 = comm.CreateParameter();
                p6.ParameterName = "@OrderNum";
                p6.SqlDbType = System.Data.SqlDbType.Int;
                p6.Value = randNum;
                comm.Parameters.Add(p6);

                Console.WriteLine("8) Number of rows affected = " + comm.ExecuteNonQuery() + "\n");

                //closing connection
                //Console.WriteLine("Connection closing");
            }

            Console.WriteLine($"9) Authorisation result {authorisationFlag} saved to file\n");

            if (authorisationFlag == false)
            {
                //authoriation has failed return to paymentService
                //end payment process and  print out failure receipt.

                Console.WriteLine("\n***** Authorisation Check has failed. *****\n");
                return DiagnosticErrMsg.NOTOK;
            }
            else
            {
                Console.WriteLine("10) ***** Payment Authorisation Check has passed. Wait for Order Number. *****\n");
            }

            /*************************************************************************************************
             * TODO
             * 
             * AUTHORISATION check has passed
             *
             *  Connect to the Database
             *  Pass the successful authorisation result to the database for the transaction
             *  TODO process payment, call stored procedure MarkAsPaid
             *  Wait for an order response from the transaction
             *  if orderID is not null run the settlement
             * (will use a local database to simulate this response during dev)
             * 
             **************************************************************************************************/

            // Order number and Tax values will be retieved from the database
            string orderNumResponse = string.Empty;


            //wait 3 seconds for reply from payment
            Thread.Sleep(3000);


            /*************************************************************************************
            * check database to see if the transaction has returned an order number if it has 
            * do the settlement and add the order number to the receipt
            * orderNumResponse = ReadResponseFile();
            * 
            * open database:
            * 
            **************************************************************************************/
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = connectionString;
                Console.WriteLine($"11) Open database to check for the Order number\n");
                conn.Open();
                SqlCommand comm = new SqlCommand($"SELECT OrderNum, TaxTotal, IncTax, ExTax from { tableName }", conn);

                SqlDataReader rdr = comm.ExecuteReader();

                if (rdr.Read())
                {
                    //Console.WriteLine(rdr.GetString(0));
                    //get the first value in the reader
                    orderNumResponse = rdr.GetString(0);
                    total = float.Parse(rdr.GetString(1));
                    incTax = float.Parse(rdr.GetString(2));
                    exTax = float.Parse(rdr.GetString(3));

                }
                else
                {
                    Console.WriteLine("not available yet");
                    orderNumResponse = string.Empty;
                }
               // Console.WriteLine("Database Closing");
            }

            Console.WriteLine("12) Order number returned = " + orderNumResponse);

            // Add the Order number and VAT values to the receipts

            string orderNumber = $"\nORDER NUMBER : {orderNumResponse}\n";
            string taxValues = "Tax = " + tax + " \n" +
                               "Including Tax = " + incTax + " \n" +
                               "Excluding Tax = " + exTax + " \n\n";

            /********************************************************************************
             * 
             * Submittal using settlement Reference                                 
             *   
             * ******************************************************************************/

            //test code remove from final code
            if (orderNumResponse == "0")
            {
                //set order num to null
                orderNumResponse = null;
                Console.WriteLine("\n************ No order number returned - Void Transaction ************\n");
            }

            /************************************************************************************
            * 
            * Check the transaction returns an order number and settle the transaction
            * if not void the transaction
            * 
            * **********************************************************************************/

            if ((isSuccessful == DiagnosticErrMsg.OK) && (authorisationFlag == true) && (!(string.IsNullOrEmpty(orderNumResponse))))
            {
                /****************************************************************************
                 * Submittal using settlement Reference, amount , transNum
                 * description which is the transaction reference
                 * 
                 * Submit Settlement Payment                                                           
                 ****************************************************************************/
                Console.WriteLine("\n13) ******Performing Settlement Payment ******\n");

                paymentSettlementXml = smartpayOps.PaymentSettle(amount, transNum, reference, description, currency, country);

                // open paymentXml socket connection
                Socket paymenSettlementSocket = CreateSocket();

                //check socket open
                //Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymenSettlementSocket));

                //send submitpayment to smartpay - check response
                string paymentSettleResponseStr = sendToSmartPay(paymenSettlementSocket, paymentSettlementXml, "SUBMITPAYMENT");


                //check response from Smartpay is not Null or Empty
                if (CheckIsNullOrEmpty(paymentSettleResponseStr, "Settlement Payment")) isSuccessful = DiagnosticErrMsg.NOTOK;
                else
                {
                    submitSettlePaymentResult = CheckResult(paymentSettleResponseStr);

                    if (submitSettlePaymentResult == "success")
                    {
                        Console.WriteLine("14) ******Successful Settlement Payment submitted******\n");
                    }
                    else
                    {
                        Console.WriteLine("****** Settlement Payment failed******\n");
                        isSuccessful = DiagnosticErrMsg.NOTOK;
                    }
                }

                //Console.WriteLine("paymenSettlementSocket Open: " + SocketConnected(paymenSettlementSocket));

                /****************************************************************************
                * Process the settlement transaction                                        *
                *                                                                           *
                *****************************************************************************/
                Socket processSettleSocket = CreateSocket();

                //Console.WriteLine("processSettleSocket Socket Open: " + SocketConnected(processSettleSocket));

                procSettleTranXML = smartpayOps.ProcessTransaction(transNum);
                //send processTransaction - check response

                string processSettleTranResponseStr = sendToSmartPay(processSettleSocket, procSettleTranXML, "PROCESSSETTLETRANSACTION");
                //check response from Smartpay is not Null or Empty
                if (CheckIsNullOrEmpty(processSettleTranResponseStr, "Process Settlement Transaction")) isSuccessful = DiagnosticErrMsg.NOTOK;
                //No response needed

                //Console.WriteLine("processSettleSocket Socket Open: " + SocketConnected(processSettleSocket));

                /****************************************************************************
                 * Procees the Settlement finalise transaction                               *
                 *                                                                           *
                 *****************************************************************************/
                Socket finaliseSettSocket = CreateSocket();

               // Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSettSocket));
                finaliseSettleXml = smartpayOps.Finalise(transNum);

                //check response
                string finaliseSettleStr = sendToSmartPay(finaliseSettSocket, finaliseSettleXml, "FINALISE");
                if (CheckIsNullOrEmpty(finaliseSettleStr, "Settlement Finalise")) isSuccessful = DiagnosticErrMsg.NOTOK;
                else
                {
                    finaliseSettleResult = CheckResult(finaliseSettleStr);

                    if (finaliseSettleResult == "success")
                    {
                        Console.WriteLine("\n15) ******Transaction Settle inalised successfully******\n");
                    }
                    else
                    {
                        Console.WriteLine("****** Transaction Settle not Finalised ******\n");
                        isSuccessful = DiagnosticErrMsg.NOTOK;
                    }
                }

                //Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSettSocket));

                /*****************************************************************************
                 *  If no error                                                              *
                 *                                                                           *
                 *****************************************************************************/
                if (isSuccessful == DiagnosticErrMsg.OK)
                {
                    //add the order number and the VAT to the reciepts

                    int position = transactionReceipts.CustomerReturnedReceipt.IndexOf("Please");
                    if (position >= 0)
                    {
                        //Add TAX Values and order number
                        transactionReceipts.CustomerReturnedReceipt = transactionReceipts.CustomerReturnedReceipt.Insert(position, taxValues);
                        transactionReceipts.CustomerReturnedReceipt = transactionReceipts.CustomerReturnedReceipt.Insert(0, orderNumber);
                    }
                }
            }
            else
            {
                ////////////////////////
                // void the transaction
                ////////////////////////

                Socket voidSocket = CreateSocket();
                //Console.WriteLine("void Socket Open: " + SocketConnected(voidSocket));

                voidXml = smartpayOps.VoidTransaction(transNum, sourceId, transactionRef);
                string voidResponseStr = sendToSmartPay(voidSocket, voidXml, "VOID");


                //success is not OK
                isSuccessful = DiagnosticErrMsg.NOTOK;
               // Console.WriteLine("void Socket Open: " + SocketConnected(voidSocket));
            }

            return isSuccessful;
        
        }



        /// <summary>
        /// Sends XML document for each operation for smartpay to process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="operation"></param>
        /// <param name="operationStr"></param>
        /// <returns></returns>
        private string sendToSmartPay(Socket sender, XDocument operation, string operationStr)
        {
            int bytesRec = 0;
            string message = string.Empty;

            // Connect the socket to the remote endpoint. Catch any errors.  
            try
            {
                sender.Connect(remoteEP);

                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes(operation.ToString());

                // Send the data through the socket.  
                int bytesSent = sender.Send(msg);

                if ((operationStr == "PROCESSSETTLETRANSACTION"))
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        // Log.Info($"{operationStr} is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");


                        if (message.Contains("processTransactionResponse"))
                        {
                            Console.WriteLine("************ Processs Settlement transaction response  received *************");
                            return message;
                        }

                    } while (message != string.Empty);

                }

                if ((operationStr == "PROCESSTRANSACTION") || (operationStr == "CUSTOMERECEIPT"))
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        // Log.Info($"{operationStr} is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");

                        //Check for a receipt - don't want to process display messages
                        if (message.Contains("posPrintReceipt")) return message;

                    } while (message.Contains("posDisplayMessage"));

                }
                if ((operationStr == "SUBMITPAYMENT") || (operationStr == "FINALISE"))
                {
                    do
                    {
                        // Receive the response from the remote device and check return
                        bytesRec = sender.Receive(bytes);
                        if (bytesRec != 0)
                        {
                            //  Log.Info($"{operationStr} is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
                            return Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        }

                    } while (bytesRec != 0);
                }

                if (operationStr == "PROCESSTRANSACTIONRESPONSE")
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        // Log.Info($"operationStr is {message}");


                        if (message.Contains("processTransactionResponse"))
                        {
                            Console.WriteLine("**** Processs transaction Called ****");
                            return message;
                        }


                    } while (message != string.Empty);

                }

                if ((operationStr == "VOID"))
                {

                    bytesRec = sender.Receive(bytes);
                    message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    Console.WriteLine($"{operationStr} is {message}");


                    if (message.Contains("CANCELLED"))
                    {
                        Console.WriteLine("****** Transaction VOID  successful *****");

                        return message;
                    }

                }

            }
            catch (ArgumentNullException ane)
            {
                Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }

            return string.Empty;
        }

        /// <summary>
        /// Checks a string for a success or failure string
        /// </summary>
        /// <param name="submitResult"></param>
        /// <returns></returns>
        private string CheckResult(string submitResult)
        {
            string result = string.Empty;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(submitResult);
            XmlNodeList nodeResult = doc.GetElementsByTagName("RESULT");

            for (int i = 0; i < nodeResult.Count; i++)
            {
                if (nodeResult[i].InnerXml == "success")
                    result = "success";
                else
                    result = "failure";
            }

            return result;
        }



        /// <summary>
        /// Gets the reference number from a string
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private string GetReferenceValue(string message)
        {
            string reference = message.Substring(message.IndexOf(toBeSearched) + toBeSearched.Length);
            StringBuilder str = new StringBuilder();

            // get everything up until the first whitespace
            int num = reference.IndexOf("date");
            reference = reference.Substring(1, num - 3);
            Console.WriteLine("Ref Returned = " + reference);
            return reference;
        }


        private Socket CreateSocket()
        {
            // Create a TCP/IP  socket.  
            Socket sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            return sender;
        }



        bool SocketConnected(Socket s)
        {
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }


        string ExtractXMLReceiptDetails(string receiptStr)
        {
            string returnedStr = string.Empty;

            var receiptDoc = new XmlDocument();
            receiptDoc.LoadXml(receiptStr);

            returnedStr = receiptDoc.GetElementsByTagName("RECEIPT")[0].InnerText;

            return returnedStr;
        }


        public bool CheckIsNullOrEmpty(string stringToCheck, string stringCheck)
        {
            if (string.IsNullOrEmpty(stringToCheck))
            {
                Console.WriteLine($" String check: {stringCheck} returned a Null or Empty value.");
                isSuccessful = DiagnosticErrMsg.NOTOK;
                return true;
            }
            return false;
        }

        public void Dispose()
        {

        }
    }

}

