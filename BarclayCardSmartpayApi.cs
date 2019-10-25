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

        // Data buffer for incoming data.
        byte[] bytes = new byte[1024];

     
        public int TransNum { get; set; } = 0;

        public void SubmitPayment(int amount)
        {
        
            Random rnd = new Random();
            TransNum = rnd.Next(1, int.MaxValue);

            Console.WriteLine("Transaction Number is ***** " + TransNum +  " *****\n\n");

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

            CreateSocket(payment, "PAYMENT");

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

        void processTransaction(int transNum)
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

            CreateSocket(processTran, "PROCESSTRANSACTION");

        }


        public void PrintReciptResponse(int transNum)
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

            CreateSocket(printReceipt, "PRINTRECEIPT");

        }


        public void Finalise(int transNum)
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

            CreateSocket(finalise, "FINALISE");
        }

        private void CreateSocket(XDocument operation, string operationStr)
        {
            try
            {
                // Establish the remote endpoint for the socket.  
                // This example uses port 8000 on the local computer.  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
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

