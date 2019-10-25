using System;
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

        //int amount
        int port = 8000;
        IPHostEntry ipHostInfo;
        IPAddress ipAddress;
        IPEndPoint remoteEP;

        // Data buffer for incoming data.
        byte[] bytes = new byte[1024];

        //transaction number
        public int TransNum { get; set; } = 0;

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

            XDocument payment = null;
            XDocument procTran = null;
            XDocument printMerchantReceipt = null;
            XDocument printCustomerReceipt = null;

            Random rnd = new Random();
            TransNum = rnd.Next(1, int.MaxValue);
            string submitPaymentResult = string.Empty;
            string FinaliseResult = string.Empty;

            Console.WriteLine("Transaction Number is ***** " + TransNum +  " *****\n\n");

            //************ PROCEDURES ***********
            //
            //SUBMITTAL

            payment = Payment(amount);

            // open payment socket connection
            Socket paymentsocket = CreateSocket();

            //check socket open
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

            //send submitpayment to smartpay - check response
            string paymentResponse = sendToSmartPay(paymentsocket, payment, "PAYMENT");
           
            submitPaymentResult = CheckResult(paymentResponse);
            
            if (submitPaymentResult == "success") Console.WriteLine("******Successful payment submitted******\n");
            else
                Console.WriteLine("****** Payment failed******\n");
       
            //checkSocket closed
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

          
            // TRANSACTIONAL
            // open processTransactionsocket connection
            Socket processSocket = CreateSocket();
            //check socket open
            Console.WriteLine("ProcessTransaction Socket Open: " + SocketConnected(processSocket));
            procTran = processTransaction(TransNum);
            //send processTransaction - check response
            string processTran = sendToSmartPay(processSocket, procTran, "PROCESSTRANSACTION");
            Console.WriteLine($"ProcessTran Return: {processTran}");
            //checkSocket closed
            Console.WriteLine("ProcessTransaction Open: " + SocketConnected(paymentsocket));

            //INTERACTION
            // open printMerchantReceiptSocket connection
            Socket printMerchantReceiptSocket = CreateSocket();
            //check socket open
            Console.WriteLine("printMerchantReceipt Socket Open: " + SocketConnected(printMerchantReceiptSocket));
            printMerchantReceipt = PrintReciptResponse(TransNum);
            //send printMerchantReceipt - check response
            string printMerchant = sendToSmartPay(printMerchantReceiptSocket, printMerchantReceipt, "PRINTRECEIPT");
            Console.WriteLine($"printMerchant Return: {printMerchant}");
            Console.WriteLine("printMerchantReceipt Socket Open: " + SocketConnected(printMerchantReceiptSocket));

            //INTERACTION
            //open Customer Receipt connection
            Socket printCustomerReceiptSocket = CreateSocket();
            //check socket open
            Console.WriteLine("printCustomerReceipt Socket Open: " + SocketConnected(printCustomerReceiptSocket));
            printCustomerReceipt = PrintReciptResponse(TransNum);
            //send printCustomerReceipt - check response
            string printCustomer = sendToSmartPay(printCustomerReceiptSocket, printCustomerReceipt, "PRINTRECEIPTCUSTOMER");
            Console.WriteLine($"printCustomer Return: {printCustomer}");
            Console.WriteLine("printCustomerReceipt Socket Open: " + SocketConnected(printCustomerReceiptSocket));

            //FINALISE
            //open Finalisesocket connection
            Socket finaliseSocket = CreateSocket();
            //check socket open
            Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));
            printCustomerReceipt = Finalise(TransNum);
            //check response
            string finaliseStr = sendToSmartPay(finaliseSocket, printCustomerReceipt, "FINALISE");
            FinaliseResult = CheckResult(finaliseStr);

            if (FinaliseResult == "success") Console.WriteLine("******Transaction Finalised successfully******\n");
            else
                Console.WriteLine("****** Transaction not Finalised ******\n");
            Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));

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


                if ((operationStr == "PROCESSTRANSACTION") || (operationStr == "PRINTRECEIPT"))
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
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
                            Console.WriteLine($"Response is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
                            return Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        }

                    } while (bytesRec == 0);
                }

                if (operationStr == "PRINTRECEIPTCUSTOMER")
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (message.Contains("processTransactionResponse")) return message;

                    } while (bytesRec == 0);


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
                //Console.WriteLine("Result: " + nodeResult[i].InnerXml);
                if (nodeResult[i].InnerXml == "success")
                    result = "success";
                else
                    result = "failure";
            }

            return result;
        }



        private string PosPrintReceiptResult(string submitResult)
        {
            string result = string.Empty;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(submitResult);
            XmlNodeList nodeResult = doc.GetElementsByTagName("RECEIPT type=\"merchant\" format=\"plain\"");

            for (int i = 0; i < nodeResult.Count; i++)
            {
                //Console.WriteLine("Result: " + nodeResult[i].InnerXml);
                if (nodeResult[i].InnerXml == "success")
                    result = "success";
                else
                    result = "failure";
            }

            return result;
        }


        public XDocument Payment(int amount)
        {
            XDocument payment = XDocument.Parse(
                                  "<RLSOLVE_MSG version=\"5.0\">" +
                                  "<MESSAGE>" +
                                  //"<SOURCE_ID>K0001</SOURCE_ID>" +
                                  "<TRANS_NUM>" + TransNum +
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

        public XDocument processTransaction(int transNum)
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

            // ProcessMessage(processTran, "PROCESSTRANSACTION");
            return processTran;

        }


        public XDocument PrintReciptResponse(int transNum)
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

           // ProcessMessage(printMerchantReceipt, "PRINTRECEIPT");
            return printReceipt;
        }


        public XDocument Finalise(int transNum)
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

            // ProcessMessage(finalise, "FINALISE");

            return finalise;
        }


        //new
        private void CheckMessage(XDocument operation, string operationStr)
        {

        }


        private string PaymentResult(string submitResult, int bytesRec)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Encoding.ASCII.GetString(bytes, 0, bytesRec).ToString());
            XmlNodeList result = doc.GetElementsByTagName("RESULT");

            for (int i = 0; i < result.Count; i++)
            {
                // Console.WriteLine("Result: " + result[i].InnerXml);
                if (result[i].InnerXml == "success")
                    submitResult = "success";

                Console.WriteLine("******Successful payment submitted******\n");
            }

            return submitResult;
        }
        //new 
        private Socket CreateSocket()
        {
            // Create a TCP/IP  socket.  
            Socket sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            return sender;
        }

       

        private void ProcessMessage(XDocument operation, string operationStr)
        {
            try
            {
                // Establish the remote endpoint for the socket.  
                // This example uses port 8000 on the local computer.  
                //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                //IPAddress ipAddress = ipHostInfo.AddressList[0];
                //IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);


                string submitResult = string.Empty;
                string tranResp = string.Empty;

                // Create a TCP/IP  socket.  
                Socket sender = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

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
                 
                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);


                    Console.WriteLine("\n" + operationStr + " :" +
                        "\n{0}",
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));

                    if (operationStr == "PAYMENT")
                    {
                        submitResult = PaymentResult(submitResult, bytesRec);
                    }
                    string outputResponse = string.Empty;
                    

                    //need to process until TransactionResponse is available so need to 
                    // check the bytes returned until the TransactionResponse is recieved
                    //and check for a merchant and customer receipt, if one is required send the transactiion
                    // for this
                    if (operationStr == "PROCESSTRANSACTION")
                    {
                        do
                        {
                            //check each response in turn
                            //Console.WriteLine($" Count:{count++} is socket connected: {SocketConnected(sender)}");

                            outputResponse = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                            //Thread.Sleep(1000);

                            //recieve each response 
                            bytesRec = sender.Receive(bytes);

                            if (outputResponse.Contains("posPrintReceipt"))
                            {
                                Console.WriteLine("Printing Receipt:\n " + outputResponse + "\n");
                                PrintReciptResponse(TransNum);
                                Console.WriteLine("Printing Receipt2:\n " + outputResponse + "\n");
                                PrintReciptResponse(TransNum);
                                //Thread.Sleep(1000);
                            }

                        } while (outputResponse != string.Empty);


                        Console.WriteLine("Finalise:\n " + outputResponse + "\n");
                        Finalise(TransNum);
                    }



                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                    if (submitResult == "success")
                    {
                       processTransaction(TransNum);
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

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
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

