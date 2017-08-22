using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Data;
using System.Data.SqlClient;

namespace MultiServer
{
    class Program
    {

        #region Statik Tanımlamalar
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        private static readonly List<Socket> clientSockets = new List<Socket>();
        private static readonly List<string> users = new List<string>();

        private const int BUFFER_SIZE = 2048;
        private const int PORT = 100;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        private static SqlConnection baglanti;
        private static SqlCommand komut;
        private static SqlDataReader reader;
        #endregion


        static void Main()
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;

            Console.SetWindowSize(70, 35);
            Console.Clear();

            Console.Title = "Mesajlaşma Server";
            SetupServer();

            while (Console.ReadLine()!="cikis") ;

            CloseAllSockets();
            
        }

        private static void SetupServer()
        {
            Console.WriteLine("Server ayarları yapılıyor.");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);

            baglanti = new SqlConnection();
            baglanti.ConnectionString = "Data Source=.;Initial Catalog=socketmesaj;Integrated Security=SSPI";
            komut = new SqlCommand();
            komut.Connection = baglanti;

            Console.WriteLine("Server hazır.");


        }
        
        private static void CloseAllSockets()
        {
            foreach (Socket socket in clientSockets)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) 
            {
                return;
            }

           clientSockets.Add(socket);
           socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
           Console.WriteLine("Istemci baglandi. Istek bekleniyor.");

            

           serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            #region Tanımlamalar
            Socket current = (Socket)AR.AsyncState;
            Console.WriteLine(current.RemoteEndPoint);
            int received;
            #endregion

            #region Try-catch Bloğu

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Istemci Baglantisini Kesti.");

                int index;
                index = clientSockets.IndexOf(current);
                if (users.Count>index) users.Remove(users[index]);
                current.Close(); 
                clientSockets.Remove(current);
                
                return;
            }

            #endregion

            #region İstemciden gelen mesaj

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);
            Console.WriteLine("Gelen Mesaj: " + text);

            #endregion

            #region Sunucudan zaman isteginde bulunma

            //if (text.ToLower() == "zaman") // sunucudan zaman talebi
            //{
            //    Console.WriteLine("Zaman istegi yapildi.");
            //    byte[] data = Encoding.ASCII.GetBytes(DateTime.Now.ToLongTimeString());
            //    current.Send(data);
            //    Console.WriteLine("Zaman istemciye gönderildi");
            //}
            //else 

            #endregion

            #region istemcinin baglanti koparmasi

            if (text.ToLower() == "cikis")
            {
                
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                int index;
                index=clientSockets.IndexOf(current);
                users.Remove(users[index]);
                clientSockets.Remove(current);


                Console.WriteLine("Istemci baglantisi koparıldı");
                return;
            }

            #endregion

            
            else
            {
                
                string[] token = text.Split(';');  //Gelen mesajı parçalama

                #region kullanıcı servera kullanıcı adını göndermişse 

                if (token[0]=="100")  
                {
                    users.Add(token[2]);
                }
                #endregion

                #region Bireysel sohbet mesajı ise
                else if (Convert.ToInt32(token[0])==1) 
                {
                    
                    sendMessage(token[1],token[2] ,token[3]);

                    Console.WriteLine("Sohbet Mesajı iletildi");
                }
                #endregion

                #region Grup sohbeti ise
                else if (Convert.ToInt32(token[0])>1)
                {
                    string kisiler;
                    

                    komut.Connection = baglanti;
                    komut.CommandText = "SELECT * FROM groups WHERE grupAd='" + token[1] + "'";
                    baglanti.Open();
                    reader = komut.ExecuteReader();

                    if (reader.Read())
                    {
                        kisiler = reader[2].ToString();
                        string[] grup = kisiler.Split(',');
                        baglanti.Close();
                        reader.Close();

                        for (int i = 0; i < Convert.ToInt32(token[0]); i++)
                        {
                            if (grup[i] == token[2]) continue;
                            sendMessage(grup[i],token[2],token[3]);

                            Console.WriteLine("Sohbet Mesajı iletildi");
                        }
                        
                    }
                    
                }

                #endregion

                #region Herkese (Yayın Mesajı ise)
                else if (Convert.ToInt32(token[0])==0)
                {
                    List<string> alici = new List<string>();


                    komut.Connection = baglanti;
                    komut.CommandText = "SELECT kadi FROM db_users";
                    baglanti.Open();
                    reader = komut.ExecuteReader();

                    while (reader.Read())
                    {
                        alici.Add(reader[0].ToString());
                    }

                    baglanti.Close();
                    reader.Close();

                    for (int i = 0; i < alici.Count; i++)
                    {
                        if (alici[i] == token[2]) continue;
                        sendMessage(alici[i], token[2], token[3]);

                        Console.WriteLine("Sohbet Mesajı iletildi");
                    }
                }

                #endregion

                
                
            }

            //İstek işlendikten sonra yeni istek bekleme
            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);

        }


        private static void sendMessage(string receive,string sender,string message)
        {
            Socket client= null;
           
            if (users.IndexOf(receive) != -1)//Gönderilen istemci aktif ise mesajı gönderir
            {

                client = clientSockets[users.IndexOf(receive)];
                byte[] data = Encoding.ASCII.GetBytes(sender + ";" + message);
                client.Send(data);
                komut.CommandText = "INSERT INTO messages (gonderen,alici,mesaj,iletim) VALUES ('" + sender + "','" + receive + "','" + message + "','1')";
            }
            else//Değilse veritabanına aktif degil girdisi girilir.
            {
                Console.WriteLine("Kullanıcı aktif degil.Online olunca mesaj iletilecektir.");
                komut.CommandText = "INSERT INTO messages (gonderen,alici,mesaj,iletim) VALUES ('" + sender + "','" + receive + "','" + message + "','0')";
            }

            baglanti.Open();
            komut.ExecuteNonQuery();
            baglanti.Close();


        }

    }
}
