// Generated by .NET Reflector from e:\IT Management\1397\Projects\تردد کامیون\Karabin (002)\KarabinEmbeddedLPRLibrary\Karabin.Embedded.LPR.Library.dll
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace KarabinEmbeddedLPRLibrary
{

    internal class NedapListener
    {
        public bool isStopped = true;
        private int port;
        private Socket WinSocket;
        private readonly object threadLock = new object();
        private List<PlateReceivedEventArgs> plates = new List<PlateReceivedEventArgs>();
        private PlateReceivedEvent _OnPlateReceived;

        public event PlateReceivedEvent OnPlateReceived
        {
            add
            {
                PlateReceivedEvent onPlateReceived = this._OnPlateReceived;
                while (true)
                {
                    PlateReceivedEvent a = onPlateReceived;
                    PlateReceivedEvent event4 = (PlateReceivedEvent)Delegate.Combine(a, value);
                    onPlateReceived = Interlocked.CompareExchange<PlateReceivedEvent>(ref this._OnPlateReceived, event4, a);
                    if (ReferenceEquals(onPlateReceived, a))
                    {
                        return;
                    }
                }
            }
            remove
            {
                PlateReceivedEvent onPlateReceived = this._OnPlateReceived;
                while (true)
                {
                    PlateReceivedEvent source = onPlateReceived;
                    PlateReceivedEvent event4 = (PlateReceivedEvent)Delegate.Remove(source, value);
                    onPlateReceived = Interlocked.CompareExchange<PlateReceivedEvent>(ref this._OnPlateReceived, event4, source);
                    if (ReferenceEquals(onPlateReceived, source))
                    {
                        return;
                    }
                }
            }
        }

        public void DisconnectSocket()
        {
            try
            {
                this.WinSocket.Disconnect(false);
            }
            catch
            {
            }
            try
            {
                this.WinSocket.Close();
            }
            catch
            {
            }
            try
            {
                this.WinSocket.Dispose();
            }
            catch
            {
            }
        }

        public void PlateReceivedEventThread()
        {
            while (true)
            {
                while (true)
                {
                    if (this.isStopped)
                    {
                        return;
                    }
                    List<PlateReceivedEventArgs> list = new List<PlateReceivedEventArgs>();
                    bool flag2 = this.plates.Count == 0;
                    if (!flag2)
                    {
                        PlateReceivedEventArgs current;
                        List<PlateReceivedEventArgs>.Enumerator enumerator;
                        lock (this.plates)
                        {
                            using (enumerator = this.plates.GetEnumerator())
                            {
                                while (true)
                                {
                                    flag2 = enumerator.MoveNext();
                                    if (!flag2)
                                    {
                                        break;
                                    }
                                    current = enumerator.Current;
                                    list.Add(current);
                                }
                            }
                            this.plates.Clear();
                        }
                        using (enumerator = list.GetEnumerator())
                        {
                            while (true)
                            {
                                flag2 = enumerator.MoveNext();
                                if (!flag2)
                                {
                                    break;
                                }
                                current = enumerator.Current;
                                this._OnPlateReceived(this, current);
                            }
                        }
                    }
                    break;
                }
                Thread.Sleep(20);
            }
        }

        public void Start(int port)
        {
            if (this.isStopped)
            {
                this.port = port;
                this.isStopped = false;
                new Thread(new ThreadStart(this.TCPServerThread)) { Name = "TCPServerThread" }.Start();
                new Thread(new ThreadStart(this.PlateReceivedEventThread)) { Name = "PlateReceivedEventThread" }.Start();
            }
        }

        public void Stop()
        {
            if (!this.isStopped)
            {
                this.isStopped = true;
                Thread.Sleep(30);
                this.DisconnectSocket();
            }
        }

        private void TCPServerThread()
        {
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, this.port);
            this.WinSocket = null;
            while (true)
            {
                bool flag2 = !this.isStopped;
                if (flag2)
                {
                    Exception exception;
                    Thread.Sleep(0x3e8);
                    try
                    {
                        try
                        {
                            flag2 = ReferenceEquals(this.WinSocket, null);
                            if (!flag2)
                            {
                                this.WinSocket.Close();
                            }
                        }
                        catch (Exception exception1)
                        {
                            exception = exception1;
                        }
                        this.WinSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        this.WinSocket.Bind(localEP);
                        this.WinSocket.Listen(10);
                        while (true)
                        {
                            List<PlateReceivedEventArgs> list;
                            flag2 = !this.isStopped;
                            if (!flag2)
                            {
                                break;
                            }
                            Socket socket = this.WinSocket.Accept();
                            byte[] buffer = new byte[13];
                            int num = socket.Receive(buffer);
                            byte[] buffer2 = new byte[50];
                            int num2 = socket.Receive(buffer2);
                            bool lockTaken = false;
                            try
                            {
                                Monitor.Enter(list = this.plates, ref lockTaken);
                                this.plates.Add(new PlateReceivedEventArgs(buffer, buffer2));
                            }
                            finally
                            {
                                flag2 = !lockTaken;
                                if (!flag2)
                                {
                                    Monitor.Exit(this.plates);
                                }
                            }
                            socket.Shutdown(SocketShutdown.Both);
                            socket.Close();
                        }
                        continue;
                    }
                    catch (SocketException)
                    {
                        flag2 = !this.isStopped;
                        if (flag2)
                        {
                            this.DisconnectSocket();
                            continue;
                        }
                    }
                    catch (Exception exception4)
                    {
                        exception = exception4;
                        flag2 = !this.isStopped;
                        if (flag2)
                        {
                            continue;
                        }
                    }
                }
                this.DisconnectSocket();
                return;
            }
        }

        public delegate void PlateReceivedEvent(object source, NedapListener.PlateReceivedEventArgs e);

        public class PlateReceivedEventArgs : EventArgs
        {
            private byte[] Plate;
            private byte[] Path;

            public PlateReceivedEventArgs(byte[] plate, byte[] path)
            {
                this.Plate = plate;
                this.Path = path;
            }

            public byte[] GetPath()
            {
                return this.Path;
            }

            public byte[] GetPlate()
            {
                return this.Plate;
            }
        }
    }
}