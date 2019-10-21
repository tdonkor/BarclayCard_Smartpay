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

        //public BarclayCardSmartpayApi(int amount)
        //{
        //  //  this.amount = amount;
       
        //}

        public void SubmitPayment(int amount)
        {
            // Data buffer for incoming data.  
           // byte[] bytes = new byte[1024];
            Random rnd = new Random();
            int transNum = rnd.Next(1, int.MaxValue);
            string submitResult = string.Empty;

            XDocument payment = XDocument.Parse(
                                   "<RLSOLVE_MSG version=\"5.0\">" + "<MESSAGE>" + "<TRANS_NUM>" + transNum + "</TRANS_NUM>" + "</MESSAGE>" + "<POI_MSG type=\"submittal\">" +
                                    "<SUBMIT name=\"submitPayment\">" +
                                     "<TRANSACTION type= \"purchase\" action =\"auth\" source =\"icc\" customer=\"present\">" +
                                     "<AMOUNT currency=\"826\" country=\"826\">" +
                                       "<TOTAL>" + amount + "</TOTAL>" +
                                     "</AMOUNT>" +
                                     "</TRANSACTION>" +
                                    "</SUBMIT>" +
                                   "</POI_MSG>" +
                                 "</RLSOLVE_MSG>");
           
            try
            {
                // Establish the remote endpoint for the socket.  
                // This example uses port 8000 on the local computer.  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                // Create a TCP/IP  socket.  
                Socket sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.  
                try
                {
                    sender.Connect(remoteEP);

                    Console.WriteLine("Socket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(payment.ToString());

                    // Send the data through the socket.  
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);
                    Console.WriteLine("Payment Submitted = {0}",
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));

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

                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                    if (submitResult == "success")
                    {
                    
                        processTransaction(transNum);
                        PrintReciptResponse(transNum);
                        Finalise(transNum);
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

                // Create a TCP/IP  socket.  
                Socket sender = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.  
                try
                {
                    sender.Connect(remoteEP);

                    Console.WriteLine("\nSocket connected to:\n{0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(operation.ToString());

                    // Send the data through the socket.  
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);
                    Console.WriteLine("\n" + operationStr + ":" +
                        "\n{0}",
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));

                    if (operationStr == "PROCESSTRANSACTION")
                    {
                        Thread.Sleep(10000);
                    }


                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
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

       

        public void Dispose()
        {

        }
    }

}

