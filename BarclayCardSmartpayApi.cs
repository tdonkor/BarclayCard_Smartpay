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

        //int amount;
        string host = "127.0.0.1";
        int port = 8000;

        // Data buffer for incoming data.
        byte[] bytes = new byte[1024];

        //public BarclayCardSmartpayApi(int amount)
        //{
        //  //  this.amount = amount;
       
        //}


        //public void SocketListner()
        //{
        //    XDocument doc = XDocument.Parse(
        //               "<RLSOLVE_MSG version=\"5.0\">"+"<MESSAGE>"+"<TRANS_NUM>00001</TRANS_NUM>"+"</MESSAGE>"+"<POI_MSG type=\"submittal\">" +
        //                "<SUBMIT name=\"submitPayment\">" +
        //                 "<TRANSACTION type= \"purchase\" action =\"auth\" source =\"icc\" customer=\"present\">"+
        //                 "<AMOUNT currency=\"826\" country=\"826\">" +
        //                   "<TOTAL>" + amount + "</TOTAL>" +
        //                 "</AMOUNT>" +
        //                 "</TRANSACTION>" +
        //                "</SUBMIT>" +
        //               "</POI_MSG>" +
        //             "</RLSOLVE_MSG>");
        //    // Connect to a remote device.
        //    try
        //    {
        //        // Look up the address for the specified host.
        //        IPHostEntry address = Dns.GetHostEntry(host);
        //        IPEndPoint ipe = new IPEndPoint(address.AddressList[0], port);

        //        // Create Socket.	
        //        using (Socket sock = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
        //        {
        //            sock.Connect(ipe); // Connect to the Socket.
        //                               // Create Stream.
        //            using (NetworkStream sockStream = new NetworkStream(sock))
        //            {
        //                // Read from the XDocument.
        //                using (XmlReader reader = doc.CreateReader())
        //                {
        //                    // Copy nodes to an XmlWriter which transforms them to bytes that are written to the Stream for the Socket.
        //                    XmlWriterSettings settings = new XmlWriterSettings();
        //                    settings.Encoding = new UTF8Encoding(false, true);
        //                    using (XmlWriter writer = XmlWriter.Create(sockStream, settings))
        //                    {
        //                        while (reader.Read()) // While there is another XML node...
        //                        {
        //                            writer.WriteNode(reader, false); // Copy that node.
        //                        }
        //                    }
        //                }
        //            }
        //        }

        //        }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Error: " + ex);
        //    }
            
        //  }

        public void SubmitPayment(int amount)
        {
            // Data buffer for incoming data.  
            byte[] bytes = new byte[1024];
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
                        Console.WriteLine("Successful payment submitted");
                    }


                    //read the response

                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                    if (submitResult == "success")
                    {
                        Thread.Sleep(10);

                        processTransaction(transNum);
                        PrintReciptResponse(transNum);


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
                              "</MESSAGE>" +"<TRANS name=\"processTransaction\"></TRANS>" +
                              "<POI_MSG type=\"transactional\">" +
                                
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
                    byte[] msg = Encoding.ASCII.GetBytes(processTran.ToString());

                    // Send the data through the socket.  
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);
                    Console.WriteLine("Process Tran = {0}",
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));

                    Thread.Sleep(10000);

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

                    Console.WriteLine("\nSocket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(printReceipt.ToString());

                    // Send the data through the socket.  
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);
                    Console.WriteLine("\nReceipt = {0}",
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));


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

                    Console.WriteLine("\nSocket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(finalise.ToString());

                    // Send the data through the socket.  
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);
                    Console.WriteLine("\nFinalise = {0}",
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));


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

