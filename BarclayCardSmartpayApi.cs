﻿using System;
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

        //int amount
        int port = 8000;
        IPHostEntry ipHostInfo;
        IPAddress ipAddress;
        IPEndPoint remoteEP;

        string custPath = @"C:\Customer Payment Drivers\PaymentTestCodewithout ATP\BarclayCard_Smartpay_Connect\";
        string merchantPath = @"C:\Customer Payment Drivers\PaymentTestCodewithout ATP\BarclayCard_Smartpay_Connect\";


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

            XDocument paymentXml = null;
            XDocument procTranXML = null;
            XDocument firstInteractionXML = null;
            XDocument secondInteractionXML = null;
            XDocument FinaliseXml = null;

  
            //check for a success or failure string 
            string submitPaymentResult = string.Empty;
            string FinaliseResult = string.Empty;

            Random rnd = new Random();
            TransNum = rnd.Next(1, int.MaxValue);

            Console.WriteLine("Transaction Number is ***** " + TransNum +  " *****\n\n");

            //************ PROCEDURES ***********
         
//SUBMITTAL
            paymentXml = Payment(amount);

            // open paymentXml socket connection
            Socket paymentsocket = CreateSocket();

            //check socket open
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

            //send submitpayment to smartpay - check response
            string paymentResponse = sendToSmartPay(paymentsocket, paymentXml, "PAYMENT");
           
            submitPaymentResult = CheckResult(paymentResponse);

            if (submitPaymentResult == "success") Console.WriteLine("******Successful paymentXml submitted******\n");
            else
            {
                Console.WriteLine("****** Payment failed******\n");
            }
       
            //checkSocket closed
            Console.WriteLine("Paymentsocket Open: " + SocketConnected(paymentsocket));

          
// TRANSACTIONAL
            // open processTransactionsocket connection
            Socket processSocket = CreateSocket();
            //check socket open
            Console.WriteLine("ProcessTransaction Socket Open: " + SocketConnected(processSocket));
            procTranXML = processTransaction(TransNum);
            //send processTransaction - check response
            string processTran = sendToSmartPay(processSocket, procTranXML, "PROCESSTRANSACTION");
            Console.WriteLine($"ProcessTran Return: {processTran}");

            //strip the result from the reciept
            string merchantResultStr = ExtractXMLReceiptDetails(processTran);

            // This text is added only once to the file.
            if (!File.Exists(merchantPath))
            {
                // Create a file to write to.
                string createText = "Hello and Welcome Merchant:" + Environment.NewLine;
                File.WriteAllText(merchantPath + "MerchantReceipt.txt", merchantResultStr);
            }
            //checkSocket closed
            Console.WriteLine("ProcessTransaction Open: " + SocketConnected(paymentsocket));

//INTERACTION
            // open firstInteractionSocket connection
            Socket firstInteractionSocket = CreateSocket();
            //check socket open
            Console.WriteLine("firstInteractionXML Socket Open: " + SocketConnected(firstInteractionSocket));
            firstInteractionXML = PrintReciptResponse(TransNum);


            string firstInteractionStr = sendToSmartPay(firstInteractionSocket, firstInteractionXML, "PROCESSTRANRESPONSE");

            //strip the result from the reciept
            string customerResultStr = ExtractXMLReceiptDetails(firstInteractionStr);

            if (!File.Exists(custPath))
            {
                // Create a file to write to.
                string createText = "Hello and Welcome Customer:" + Environment.NewLine;
                File.WriteAllText(custPath + "CustomerReceipt.txt", customerResultStr);
            }
            Console.WriteLine($"firstInteractionStr Return: {firstInteractionStr}");
            Console.WriteLine("firstInteractionXML Socket Open: " + SocketConnected(firstInteractionSocket));

//INTERACTION
            //open Customer Receipt connection
            Socket secondInteractionSocket = CreateSocket();
            //check socket open
            Console.WriteLine("FinaliseXml Socket Open: " + SocketConnected(secondInteractionSocket));
            secondInteractionXML = PrintReciptResponse(TransNum);
            //send FinaliseXml - check response
            string secondInteractionStr = sendToSmartPay(secondInteractionSocket, secondInteractionXML, "PRINTRECEIPTCUSTOMER");
            Console.WriteLine($"secondInteractionStr Return: {secondInteractionStr}");
            Console.WriteLine("secondInteractionXML Socket Open: " + SocketConnected(secondInteractionSocket));

            

//FINALISE
            //open Finalisesocket connection
            Socket finaliseSocket = CreateSocket();
            //check socket open
            Console.WriteLine("Finalise Socket Open: " + SocketConnected(finaliseSocket));
            FinaliseXml = Finalise(TransNum);
            //check response
            string finaliseStr = sendToSmartPay(finaliseSocket, FinaliseXml, "FINALISE");
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


                if ((operationStr == "PROCESSTRANSACTION") || (operationStr == "PROCESSTRANRESPONSE"))
                {
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        Console.WriteLine($"PROCESSTRANSACTION and PROCESSTRANRESPONSE is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
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
                            Console.WriteLine($"PAYMENT and FINALISE is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
                            return Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        }

                    } while (bytesRec != 0);
                }
                //TODO check for card 
                if (operationStr == "PRINTRECEIPTCUSTOMER")
                {
                   
                    do
                    {
                        bytesRec = sender.Receive(bytes);
                        message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                       // Console.WriteLine($"PRINTRECEIPTCUSTOMER is {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");
                     

                        if (message.Contains("processTransactionResponse"))
                        {
                            Console.WriteLine("************ Processs transaction Called *************");
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


        public XDocument Payment(int amount)
        {
            XDocument payment = XDocument.Parse(
                                  "<RLSOLVE_MSG version=\"5.0\">" +
                                  "<MESSAGE>" +
                                  "<SOURCE_ID>DK01ACRELEC</SOURCE_ID>" +
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


            return finalise;
        }

        //new 
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

