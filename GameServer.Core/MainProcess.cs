﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using GameServer.Maps;
using GameServer.Data;
using GameServer.Networking;

namespace GameServer
{
    public static class MainProcess
    {
        public static DateTime CurrentTime;
        public static DateTime NextUpdateLoopCountsTime;
        public static DateTime NextSaveDataTime;
        public static ConcurrentQueue<GMCommand> CommandsQueue;
        public static uint LoopCount;
        public static bool Running;
        public static bool Saving;
        public static Thread MainThread;
        public static Random RandomNumber;

        static MainProcess()
        {
            CurrentTime = DateTime.Now;
            NextUpdateLoopCountsTime = DateTime.Now.AddSeconds(1.0);
            RandomNumber = new Random();
        }

        public static void Start()
        {
            if (!Running)
            {
                Thread thread = new(new ThreadStart(MainLoop))
                {
                    IsBackground = true
                };
                MainThread = thread;
                thread.Start();
            }
        }

        public static void Stop()
        {
            Running = false;
            NetworkServiceGateway.Stop();
        }

        public static void AddSystemLog(string text)
        {
            SEnvir.AddSystemLog(text);
        }

        public static void AddChatLog(string preffix, byte[] text)
        {
            SEnvir.AddChatLog(preffix, text);
        }

        private static void MainLoop()
        {
            CommandsQueue = new ConcurrentQueue<GMCommand>();
            SEnvir.AddSystemLog("Map elements are being generated...");
            MapGatewayProcess.Start();
            SEnvir.AddSystemLog("Network services are being started...");
            NetworkServiceGateway.Start();
            SEnvir.AddSystemLog("The server has been successfully opened");
            Running = true;

            while (true)
            {
                if (!Running)
                {
                    if (NetworkServiceGateway.Connections.Count == 0)
                        break;
                    continue;
                }

                try
                {
                    CurrentTime = DateTime.Now;
                    ProcessSaveData();
                    ProcessServerStats();
                    ProcessGMCommands();
                    NetworkServiceGateway.Process();
                    MapGatewayProcess.Process();
                }
                catch (Exception ex)
                {
                    SEnvir.AddSystemLog("A fatal error has occurred and the server is about to stop");
                    if (!Directory.Exists(".\\Log\\Error"))
                        Directory.CreateDirectory(".\\Log\\Error");
                    File.WriteAllText(string.Format(".\\Log\\Error\\{0:yyyy-MM-dd--HH-mm-ss}.txt", DateTime.Now), "Error message:\r\n" + ex.Message + "\r\nStack information:\r\n" + ex.StackTrace);
                    SEnvir.AddSystemLog("Error has been saved to the log, please note");
                    ClearConnections();
                    break;
                }
            }

            SEnvir.AddSystemLog("ItemData is being cleaned up...");
            MapGatewayProcess.CleanUp();
            SEnvir.AddSystemLog("Customer data is being saved...");
            GameDataGateway.CleanUp();
            MainThread = null;
            SEnvir.AddSystemLog("The server has been successfully shut down");
        }

        private static void ClearConnections()
        {
            foreach (SConnection connection in NetworkServiceGateway.Connections)
            {
                try
                {
                    TcpClient tcpClient = connection.Connection;
                    if (tcpClient != null)
                    {
                        Socket client = tcpClient.Client;
                        if (client != null)
                        {
                            client.Shutdown(SocketShutdown.Both);
                        }
                        tcpClient.Close();
                    }
                }
                catch
                {
                }
            }
        }

        private static void ProcessGMCommands()
        {
            while (CommandsQueue.TryDequeue(out GMCommand GMCommand))
                GMCommand.Execute();
        }

        private static void ProcessServerStats()
        {
            if (CurrentTime > NextUpdateLoopCountsTime)
            {
                LoopCount = 0U;
                NextUpdateLoopCountsTime = CurrentTime.AddSeconds(1.0);
            }
            else
            {
                LoopCount += 1U;
            }
        }

        private static void ProcessSaveData()
        {
            if (NextSaveDataTime > CurrentTime) return;
            GameDataGateway.SaveData();
            GameDataGateway.PersistData();
            NextSaveDataTime = CurrentTime.AddSeconds(60);
        }
    }
}