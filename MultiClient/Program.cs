using System;
using System.Data;
using System.Data.SqlClient;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;



namespace MultiClient
{
    class Program
    {
        private static readonly Socket ClientSocket = new Socket
            (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static Thread thread1 = new Thread(new ThreadStart(receive));

        private const int PORT = 100;
        private const string serverIP = "192.168.1.101";

        private static int pieceUser;
        private static string RecUser,SendUser;

        private static SqlConnection baglanti;
        private static SqlCommand komut;
        private static SqlDataReader reader;
        

        static void Main()
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;

            Console.SetWindowSize(70, 35);
            Console.Clear();

            Console.Title = "Mesajlaşma Istemcisi";
            
            ConnectToServer(serverIP);
            baglanti = new SqlConnection();
            //  baglanti.ConnectionString = "Data Source=192.168.1.101,1433;Network Library=DBMSSOCN;Initial Catalog=socketmesaj;User ID=bervan;Password=cod_8580;";
            baglanti.ConnectionString = "Server="+serverIP+";Database=socketmesaj;User Id=bervan;Password = cod_8580; ";
            komut = new SqlCommand();
            komut.Connection = baglanti;

            userlogin();
            menu();

           
        }

        private static void userlogin()
        {
            string kullanici, pass;

            Console.Write("Kullanıcı Adınız :");
            kullanici = Console.ReadLine();
            Console.Write("Şifreniz :");
            pass = Console.ReadLine();


            komut.CommandText = "SELECT * FROM db_users WHERE kadi='" + kullanici + "'AND sifre='" + pass + "'";

            baglanti.Open();
            reader = komut.ExecuteReader();

            if (reader.Read())
            {
                Console.WriteLine("Giriş Başarılı\nDevam etmek için bir tuşa basınız..");
                Console.ReadKey();
                Console.Clear();
                SendUser = kullanici;
                sendUserName(SendUser);
                baglanti.Close();
                offline_messages();
            }

            else
            {
                Console.WriteLine("Kullanıcı Adı veya Şifre yanlış!!!");
                baglanti.Close();
                userlogin();
            }


            


        }

        private static void sendUserName(string text)
        {
            text = "100" + ";" + " " + ";" + SendUser + ";" + " ";
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        private static void offline_messages()
        {
            SqlCommand guncelle= new SqlCommand();
            guncelle.Connection = baglanti;
            komut.CommandText = "SELECT * FROM messages WHERE alici='" + SendUser + "'AND iletim='" + 0 + "'";

            baglanti.Open();
            reader = komut.ExecuteReader();

            Console.Clear();
            Console.WriteLine("Okunmamış mesajlar \n");

            while (reader.Read())
            {
                Console.WriteLine("{0} : {1}",reader[1],reader[3]);
            }
            reader.Close();
            guncelle.CommandText = "UPDATE messages SET iletim='1' WHERE alici='"+ SendUser+"'";
            guncelle.ExecuteNonQuery();
            baglanti.Close();
            Console.ReadKey();
            Console.Clear();
        }

        private static void menu()
        {
            int secim;

            Console.WriteLine("Anlık Mesajlaşma v1.0\nMenü\n");
            Console.WriteLine("1-)Bireysel Mesajlaşma");
            Console.WriteLine("2-)Grupla Mesajlaşma");
            Console.WriteLine("3-)Herkese Mesaj At");

            Console.Write("\nSeçiminiz :");
            secim=Convert.ToInt32(Console.ReadLine());
            
            switch (secim)
            {
                case 1:
                    singleMessage();
                    break;
                case 2:
                    groupMessage();
                    break;
                case 3:
                    sendEveryone();
                    break;
                default:
                    Console.WriteLine("Hatalı Seçim!!");
                    Console.ReadKey();
                    Console.Clear();
                    menu();
                    break;
            }
        }
        
        private static void singleMessage()
        {
            string AliciAd;
            Console.WriteLine("Alıcı adını giriniz :");
            AliciAd = Console.ReadLine();

            komut.CommandText = "SELECT * FROM db_users WHERE kadi='" + AliciAd + "'";

            baglanti.Open();
            reader = komut.ExecuteReader();

            if (reader.Read())
            {
                Console.WriteLine("Bağlantı kuruldu..");
                Console.ReadKey();
                Console.Clear();
            }
            else
            {
                Console.WriteLine("Böyle bir kullanıcı bulunamadı.\n Kullanıcı adını tekrar giriniz..");
                Console.ReadKey();Console.Clear();

                baglanti.Close();
                singleMessage();
                return;
            }


            baglanti.Close();
            
            RecUser = AliciAd;
            Console.Title = "Bireysel Sohbet - " + SendUser + " >> " + RecUser;
            pieceUser = 1;
            RequestLoop();
            Exit();

        }

        private static void groupMessage()
        {
            int secim;
            string group_name,kisiler;
            Console.WriteLine("Grup Sohbeti\n1-)Yeni Grup Oluştur\n2-)Var olan gruba yaz.");
            Console.Write("Seciminiz :");
            secim = Convert.ToInt32(Console.ReadLine());

            #region Grup Sohbeti Menüsü
            if (secim==1)
            {
                Console.Write("Grup adı :");
                group_name=Console.ReadLine();
                Console.WriteLine("Gruba eklemek istediğiniz kişiler (aralarına virgül koyun)");
                kisiler = Console.ReadLine();
                kisiler += "," + SendUser;

                komut.Connection = baglanti;
                komut.CommandText = "INSERT INTO groups (grupAd,kisiler) VALUES ('" + group_name + "','" + kisiler + "')";
                baglanti.Open();

                int sonuc = komut.ExecuteNonQuery();
                baglanti.Close();

                if (sonuc > 0) Console.WriteLine("Grup Başarıyla oluşturuldu");
                else { Console.WriteLine("Grup oluşturma başarısız"); Console.ReadKey(); return; }
                Console.ReadKey();
                Console.Clear();
            }

            else if (secim==2)
            {
                while (true)
                {
                    Console.Write("Grup adı :");
                    group_name = Console.ReadLine();

                    komut.Connection = baglanti;
                    komut.CommandText = "SELECT * FROM groups WHERE grupAd='" + group_name + "'";
                    baglanti.Open();
                    reader = komut.ExecuteReader();
                    if (reader.Read())
                    {
                        kisiler = reader[2].ToString() ;
                        baglanti.Close();
                        break;
                    }
                    Console.WriteLine("Grup Bulunamadı!!");
                    reader.Close();
                    baglanti.Close();
                }
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Hatalı Seçim!!");
                groupMessage();
                return;
            }
            #endregion


            Console.Clear();
            string[] temp = kisiler.Split(',');
            Console.Title = "Grup Sohbeti - " + SendUser + " >> " + group_name;
            pieceUser = temp.Length;
            RecUser = group_name;
            RequestLoop();
            Exit();

        }

        private static void sendEveryone()
        {
            pieceUser = 0; //herkese gönderilecegini belirtiyoruz

            RecUser = "Herkes";
            Console.Title = "Herkese Yayın Mesajı - " + SendUser + " >> Herkese" ;
            RequestLoop();
            Exit();
        }

        private static void ConnectToServer(string serverIP)
        {
            int attempts = 0;

            while (!ClientSocket.Connected)
            {
                try
                {
                    attempts++;
                    Console.WriteLine("Tekrar Bağlanıyor ({0})" , attempts);
                    
                    ClientSocket.Connect(IPAddress.Parse(serverIP), PORT);
                }
                catch (SocketException) 
                {
                    Console.Clear();
                }
            }

            Console.Clear();
            Console.WriteLine("Sunucuya Bağlandı.");
        }

        private static void RequestLoop()
        {
            Console.WriteLine(@"<Uygulamayı sonlandırmak için ""cikis"" yazınız.>");
            Console.WriteLine(GetLocalIPAddress());
            
            thread1.Start();
            while (true)
            {
                SendRequest();
            }
        }

        private static void receive() {
            var buffer = new byte[2048];
            int received = ClientSocket.Receive(buffer, SocketFlags.None);
            if (received == 0) return;
            else
            {
                ReceiveResponse(buffer, received);
            }
            Thread.Sleep(500);
            receive();
        }
        
        private static void Exit()
        {
            
            SendString("cikis"); 
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
            Environment.Exit(0);
        }

        private static void SendRequest()
        {
            string request = Console.ReadLine();
            SendString(request);
            if (request.ToLower() == "cikis")
            {
                Exit();
            }
            
        }
   
        private static void SendString(string text)
        {
            if(text!="cikis")
                text = pieceUser.ToString() + ";" + RecUser + ";" + SendUser + ";" + text;

            byte[] buffer = Encoding.ASCII.GetBytes(text);
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        private static void ReceiveResponse(byte[] buffer,int received)
        {
            var data = new byte[received];
            Array.Copy(buffer, data, received);
            string text = Encoding.ASCII.GetString(data);
            string[] token = text.Split(';');

            Console.WriteLine("\n{0} :{1}",token[0],token[1]);
            
        }

        static string GetLocalIPAddress()
        {
            string ip=null;
            foreach (IPAddress adres in Dns.GetHostAddresses(Dns.GetHostName()))
            {

                ip = adres.ToString();

            }
            return ip;
        }

    }
}
