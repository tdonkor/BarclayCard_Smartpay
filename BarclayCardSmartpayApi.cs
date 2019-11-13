using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace BarclayCard_Smartpay
{


    public class BarclayCardSmartpayApi : IDisposable
    {

        static int number = 0;
        string transNum = "000000";
        int transRef = 0;

        int port = 8000;
        IPHostEntry ipHostInfo;
        IPAddress ipAddress;
        IPEndPoint remoteEP;

        string custPath = @"C:\Customer Payment Drivers\PaymentTestCodewithout ATP\BarclayCard_Smartpay_Connect\";
        string merchantPath = @"C:\Customer Payment Drivers\PaymentTestCodewithout ATP\BarclayCard_Smartpay_Connect\";

        bool receiptSuccess;


        // Data buffer for incoming data.
        byte[] bytes = new byte[1024];

        public BarclayCardSmartpayApi()
        {
            // Establish the remote endpoint for the socket.  
            // This example uses port 8000 on the local computer.  
             ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
             ipAddress = ipHostInfo.AddressList[0];
             remoteEP = new IPEndPoint(ipAddress, port);
        }

        public void TransactionProcess(int amount)
        {

            XDocument paymentXml = null;
            XDocument procTranXML = null;
            XDocument merchantSuccessXML = null;
            XDocument customerSuccessXML = null;
            XDocument finaliseXml = null;
            XDocument cancelXml = null;
            XDocument paymentSettlementXml = null;
            XDocument procSettleTranXML = null;


            int intAmount;
            receiptSuccess = true;

            Random rnd = new Random();
            transRef = rnd.Next(1, int.MaxValue);


            //check for a success or failure string 
            string submitPaymentResult = string.Empty;
            string FinaliseResult = string.Empty;
            string submitSettlePaymentResult = string.Empty;

            number++;
            transNum = number.ToString().PadLeft(6, '0');

            //check amount is valid
            intAmount = Utils.GetNumericAmountValue(amount);

            if (intAmount == 0)
            {
                throw new Exception("Error in Amount value...");
            }


            Console.WriteLine("Transaction Number is ***** " + transNum +  " *****\n\n");

            //************ PROCEDURES ***********
         
//SUBMITTAL  -- submit payment ----

            paymentXml = Payment(amount, transNum);

            // open paymentXml socket connection
            Socket paymentsocket = CreateSocket();

            //check socket open
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

            //send submitpayment to smartpay - check response
            string paymentResponseStr = sendToSmartPay(paymentsocket, paymentXml, "PAYMENT");
            Console.WriteLine($"paymentResponse Return: {paymentResponseStr}");

            submitPaymentResult = CheckResult(paymentResponseStr);

            if (submitPaymentResult == "success")
            {
                Console.WriteLine("******Successful paymentXml submitted******\n");
            }
            else
            {
                Console.WriteLine("****** Payment failed******\n");
            }
       
            //checkSocket closed
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

          
// TRANSACTIONAL  -- process transaction -- get the merchant receipt and check transaction 
            
            Socket processSocket = CreateSocket();
         
            Console.WriteLine("ProcessTransaction Socket Open: " + SocketConnected(processSocket));

            procTranXML = processTransaction(transNum);
            //send processTransaction - check response

            string processTranResponseStr = sendToSmartPay(processSocket, procTranXML, "PROCESSTRANSACTION");
            Console.WriteLine($"ProcessTran Return: {processTranResponseStr}");

            //strip the result from the merchant receipt
            string merchantResultStr = ExtractXMLReceiptDetails(processTranResponseStr);

            //Check the merchant receipt is populated
            if (merchantResultStr == string.Empty)
            {
                Console.WriteLine("No Merchant receipt returned");
                receiptSuccess = false;
            }
            else
            {
                //check if reciept has a successful transaction
                if (merchantResultStr.Contains("DECLINED"))
                {
                    Console.WriteLine("Merchant Receipt has Declined Transaction.");
                    receiptSuccess = false;
                }
            }

            //TODO if receipt is not successful cancel the transaction or send the successful response

            //if(receiptSuccess == false)
            //{
            //    //cancel the transaction
            //    Socket cancelSocket = CreateSocket();
            //    Console.WriteLine("CancelTransaction Socket Open: " + SocketConnected(cancelSocket));
            //    cancelXml = CancelTransaction(transNum);

            //}


            //This text is added only once to the file.
            if (!File.Exists(merchantPath))
            {
                // Create a file to write to.
                string createText = "Hello and Welcome Merchant:" + Environment.NewLine;
                File.WriteAllText(merchantPath + "MerchantReceipt.txt", merchantResultStr);
            }

            Console.WriteLine("ProcessTransaction Open: " + SocketConnected(paymentsocket));

//INTERACTION  if transaction succeesful and merchant receipt returned check for customer receipt send Merchant success response.

            // open merchantSuccessSocket connection
            Socket merchantSuccessSocket = CreateSocket();
            //check socket open
            Console.WriteLine("merchantSuccessXML Socket Open: " + SocketConnected(merchantSuccessSocket));
            merchantSuccessXML = PrintReciptResponse(transNum);


            string customerResultStr = sendToSmartPay(merchantSuccessSocket, merchantSuccessXML, "MERCHANTTRECEIPT");
            Console.WriteLine($"customerResult Return: {customerResultStr}");

            //strip the result from the returned Customer receipt
            string customerReceipt = ExtractXMLReceiptDetails(customerResultStr);

            if (string.IsNullOrEmpty(customerReceipt))
            {
                Console.WriteLine("No Customer receipt returned");
            }
            else
            {
                //check if reciept has a successful transaction
                if (customerReceipt.Contains("DECLINED"))
                {
                    Console.WriteLine("Customer Receipt has Declined Transaction.");
           
                }
            }

            if (!File.Exists(custPath))
            {
                // Create a file to write to.
                string createText = "Hello and Welcome Customer:" + Environment.NewLine;
                File.WriteAllText(custPath + "CustomerReceipt.txt", customerReceipt);
            }
            Console.WriteLine($"customerResultStr Return: {customerResultStr}");
            Console.WriteLine("merchantSuccessXML Socket Open: " + SocketConnected(merchantSuccessSocket));

//INTERACTION
            //send a successful response to the customer ticket
            Socket customerSuccessSocket = CreateSocket();
          
            Console.WriteLine("customerSuccessSocket Socket Open: " + SocketConnected(customerSuccessSocket));
            customerSuccessXML = PrintReciptResponse(transNum);
         
            string customerReceiptStr = sendToSmartPay(customerSuccessSocket, customerSuccessXML, "CUSTOMERRECEIPT");
            Console.WriteLine($"customerReceipt Return: {customerReceiptStr}");

            Console.WriteLine("customerSuccessXML Socket Open: " + SocketConnected(customerSuccessSocket));

            

//FINALISE
            //open Finalisesocket connection
            Socket finaliseSocket = CreateSocket();
            //check socket open
            Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));
            finaliseXml = Finalise(transNum);
            //check response
            string finaliseStr = sendToSmartPay(finaliseSocket, finaliseXml, "FINALISE");
            Console.WriteLine($"finalise Return: {finaliseStr}");


            FinaliseResult = CheckResult(finaliseStr);

            if (FinaliseResult == "success")
            {
                Console.WriteLine("******Transaction Finalised successfully******\n");
            }
            else
            {
                Console.WriteLine("****** Transaction not Finalised ******\n");
            }

            Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));

// run the settlement by reference  SUBMITTAL-- submit payment ----

           paymentSettlementXml = Payment(amount, transNum);

            // open paymentXml socket connection
            Socket paymenSettlementSocket = CreateSocket();

            //check socket open
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

            //send submitpayment to smartpay - check response
            string paymentSettleResponseStr = sendToSmartPay(paymenSettlementSocket, paymentSettlementXml, "PAYMENT");
            Console.WriteLine($"payment SettleResponse Return: {paymentSettleResponseStr}");

            submitSettlePaymentResult = CheckResult(paymentSettleResponseStr);

            if (submitSettlePaymentResult == "success")
            {
                Console.WriteLine("******Successful Settlement Payment submitted******\n");
            }
            else
            {
                Console.WriteLine("****** Settlement Payment failed******\n");
            }

            //checkSocket closed
            Console.WriteLine("paymenSettlementSocket Open: " + SocketConnected(paymenSettlementSocket));

//Procees the settlement transaction

        }



        //new
        private string sendToSmartPay(Socket sender, XDocument operation, string operationStr)
        {
            int bytesRec = 0;
            string message = string.Empty;

            // Connect the socket to the remote endpoint. Catch any errors.  
            try
            {
                sender.Connect(remoteEP);
                // Console.WriteLine("Connection is active 1: " + SocketConnected(sender));

                Console.WriteLine("\nSocket connected to:\n{0}\n",
                    sender.RemoteEndPoint.ToString());

                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes(operation.ToString());

                // Send the data through the socket.  
                int bytesSent = sender.Send(msg);


                if ((operationStr == "PROCESSTRANSACTION") || (operationStr == "MERCHANTTRECEIPT"))
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        Console.WriteLine($"{operationStr} is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
                        if (message.Contains("posPrintReceipt")) return message;

                    } while (message.Contains("posDisplayMessage"));

                }
                if ((operationStr == "PAYMENT") || (operationStr == "FINALISE"))
                {
                    do
                    {
                        // Receive the response from the remote device and check return
                        bytesRec = sender.Receive(bytes);
                        if (bytesRec != 0)
                        {
                          //  Console.WriteLine($"{operationStr} is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
                            return Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        }

                    } while (bytesRec != 0);
                }
            
                if (operationStr == "CUSTOMERRECEIPT")
                {
                   
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                      //  Console.WriteLine($"{operationStr} is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");


                        if (message.Contains("processTransactionResponse"))
                        {
                            Console.WriteLine("************ Processs transaction response  received *************");
                            return message;
                        }

                    } while (message != string.Empty);

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


        public XDocument Payment(int amount, string transNum)
        {
            XDocument payment = XDocument.Parse(
                                  "<RLSOLVE_MSG version=\"5.0\">" +
                                  "<MESSAGE>" +
                                  "<SOURCE_ID>DK01.P001</SOURCE_ID>" +
                                  "<TRANS_NUM>" + transNum +
                                  "</TRANS_NUM>" +
                                  "</MESSAGE>" +
                                  "<POI_MSG type=\"submittal\">" +
                                   "<SUBMIT name=\"submitPayment\">" +
                                    "<TRANSACTION type= \"purchase\" action =\"auth\" source =\"icc\" customer=\"present\">" +
                                    "<AMOUNT currency=\"826\" country=\"826\">" +
                                      "<TOTAL>" + amount + "</TOTAL>" +
                                    "</AMOUNT>" +
                                    "</TRANSACTION>" +
                                   "</SUBMIT>" +
                                  "</POI_MSG>" +
                                "</RLSOLVE_MSG>");

            return payment;
        }

        public XDocument PaymentSettle(int amount, string transNum)
        {
            XDocument payment = XDocument.Parse(
                                  "<RLSOLVE_MSG version=\"5.0\">" +
                                  "<MESSAGE>" +
                                  "<TRANS_NUM>" + transNum +
                                  "</TRANS_NUM>" +
                                  "</MESSAGE>" +
                                  "<POI_MSG type=\"submittal\">" +
                                   "<SUBMIT name=\"submitPayment\">" +
                                    "<TRANSACTION type= \"purchase\" action =\"settle_transref\" source =\"icc\" customer=\"present\" reference= "
                                    + "\"" + transRef + "\"" + "> " +
                                    "<AMOUNT currency=\"826\" country=\"826\">" +
                                      "<TOTAL>" + amount + "</TOTAL>" +
                                    "</AMOUNT>" +
                                    "</TRANSACTION>" +
                                   "</SUBMIT>" +
                                  "</POI_MSG>" +
                                "</RLSOLVE_MSG>");

            return payment;
        }

        public XDocument processTransaction(string transNum)
        {
            XDocument processTran = XDocument.Parse(
                              "<RLSOLVE_MSG version=\"5.0\">" +
                              "<MESSAGE>" + 
                                "<TRANS_NUM>" +
                                    transNum + 
                                "</TRANS_NUM>" + 
                              "</MESSAGE>" +
                              "<POI_MSG type=\"transactional\">" +
                              "<TRANS name=\"processTransaction\"></TRANS>" +
                              "</POI_MSG>" +
                            "</RLSOLVE_MSG>");

            return processTran;

        }


        public XDocument PrintReciptResponse(string transNum)
        {
            XDocument printReceipt = XDocument.Parse(
                            "<RLSOLVE_MSG version=\"5.0\">" +
                            "<MESSAGE>" +
                              "<TRANS_NUM>" +
                                  transNum +
                              "</TRANS_NUM>" +
                            "</MESSAGE>" +
                            "<POI_MSG type=\"interaction\">" +
                              "<INTERACTION name=\"posPrintReceiptResponse\">" +
                                  "<RESPONSE>success</RESPONSE>" +
                              "</INTERACTION>" +
                            "</POI_MSG>" +
                          "</RLSOLVE_MSG>");

            return printReceipt;
        }


        public XDocument Finalise(string transNum)
        {
            XDocument finalise = XDocument.Parse(
                            "<RLSOLVE_MSG version=\"5.0\">" +
                            "<MESSAGE>" +
                              "<TRANS_NUM>" +
                                  transNum +
                              "</TRANS_NUM>" +
                            "</MESSAGE>" +
                            "<POI_MSG type=\"transactional\">" +
                             "<TRANS name=\"finalise\"></TRANS>" +
                            "</POI_MSG>" +
                          "</RLSOLVE_MSG>");


            return finalise;
        }

        public XDocument CancelTransaction(string transNum)
        {
            XDocument cancel = XDocument.Parse(
                            "<RLSOLVE_MSG version=\"5.0\">" +
                            "<MESSAGE>" +
                              "<TRANS_NUM>" +
                                  transNum +
                                  "<SOURCE_ID>DK01.P001</SOURCE_ID>" +
                              "</TRANS_NUM>" +
                            "</MESSAGE>" +
                            "<POI_MSG type=\"administrative\">" +
                             "<TRANSACTION reference=\"cancelTransaction\"></TRANSACTION>" +
                            "</POI_MSG>" +
                          "</RLSOLVE_MSG>");


            return cancel;
        }

        private Socket CreateSocket()
        {
            // Create a TCP/IP  socket.  
            Socket sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            return sender;
        }

       
        string ExtractXMLReceiptDetails(string receiptStr)
        {
            string returnedStr = string.Empty;

            var receiptDoc = new XmlDocument();
            receiptDoc.LoadXml(receiptStr);

            returnedStr = receiptDoc.GetElementsByTagName("RECEIPT")[0].InnerText;

            return returnedStr;
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

        public void Dispose()
        {

        }
    }

}

