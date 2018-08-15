﻿using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Barotrauma.Networking
{
    class UnauthenticatedClient
    {
        public NetConnection Connection;
        public int Nonce;

        public int failedAttempts;

        public float AuthTimer;

        public UnauthenticatedClient(NetConnection connection, int nonce)
        {
            Connection = connection;
            Nonce = nonce;

            AuthTimer = 10.0f;

            failedAttempts = 0;
        }
    }

    partial class GameServer : NetworkMember, ISerializableEntity
    {
        List<UnauthenticatedClient> unauthenticatedClients = new List<UnauthenticatedClient>();

        private void ClientAuthRequest(NetConnection conn)
        {
            //client wants to know if server requires password
            if (ConnectedClients.Find(c => c.Connection == conn) != null)
            {
                //this client has already been authenticated
                return;
            }

            GameMain.NilMod.RecheckPlayerCounts();

            UnauthenticatedClient unauthClient = unauthenticatedClients.Find(uc => uc.Connection == conn);
            if (unauthClient == null)
            {
                //Clients permissions
                var precheckPermissions = clientPermissions.Find(cp => cp.IP == conn.RemoteEndPoint.Address.ToString());
                int nonce;

                //new client, generate nonce and add to unauth queue
                if ((GameMain.NilMod.CurrentPlayers + unauthenticatedClients.Count) >= maxPlayers)
                {
                    if (precheckPermissions.OwnerSlot && GameMain.NilMod.Owners + 1 <= GameMain.NilMod.MaxOwnerSlots)
                    {
                        nonce = CryptoRandom.Instance.Next();
                        unauthClient = new UnauthenticatedClient(conn, nonce);
                        unauthenticatedClients.Add(unauthClient);
                    }
                    else if (precheckPermissions.AdministratorSlot && GameMain.NilMod.Admins + 1 <= GameMain.NilMod.MaxAdminSlots)
                    {
                        nonce = CryptoRandom.Instance.Next();
                        unauthClient = new UnauthenticatedClient(conn, nonce);
                        unauthenticatedClients.Add(unauthClient);
                    }
                    else if (precheckPermissions.TrustedSlot && GameMain.NilMod.Trusted + 1 <= GameMain.NilMod.MaxTrustedSlots)
                    {
                        nonce = CryptoRandom.Instance.Next();
                        unauthClient = new UnauthenticatedClient(conn, nonce);
                        unauthenticatedClients.Add(unauthClient);
                    }
                    else
                    {
                        //server is full, can't allow new connection
                        conn.Disconnect("Server full");
                        return;
                    }
                }
                else
                {
                    nonce = CryptoRandom.Instance.Next();
                    unauthClient = new UnauthenticatedClient(conn, nonce);
                    unauthenticatedClients.Add(unauthClient);
                }
            }
            unauthClient.AuthTimer = 10.0f;
            //if the client is already in the queue, getting another unauth request means that our response was lost; resend
            NetOutgoingMessage nonceMsg = server.CreateMessage();
            nonceMsg.Write((byte)ServerPacketHeader.AUTH_RESPONSE);
            if (string.IsNullOrEmpty(password))
            {
                nonceMsg.Write(false); //false = no password
            }
            else
            {
                nonceMsg.Write(true); //true = password
                nonceMsg.Write((Int32)unauthClient.Nonce); //here's nonce, encrypt with this
            }
            server.SendMessage(nonceMsg, conn, NetDeliveryMethod.Unreliable);
        }

        private void ClientInitRequest(NetIncomingMessage inc)
        {
            Boolean isNilModClient = false;

            if (ConnectedClients.Find(c => c.Connection == inc.SenderConnection) != null)
            {
                //this client was already authenticated
                //another init request means they didn't get any update packets yet
                return;
            }

            UnauthenticatedClient unauthClient = unauthenticatedClients.Find(uc => uc.Connection == inc.SenderConnection);
            if (unauthClient == null)
            {
                //client did not ask for nonce first, can't authorize
                inc.SenderConnection.Disconnect("Client (" + unauthClient.Connection.RemoteEndPoint.Address.ToString() + ") did not properly request authentication.");
                return;
            }

            if (!string.IsNullOrEmpty(password))
            {
                //decrypt message and compare password
                string saltedPw = password;
                saltedPw = saltedPw + Convert.ToString(unauthClient.Nonce);
                saltedPw = Encoding.UTF8.GetString(NetUtility.ComputeSHAHash(Encoding.UTF8.GetBytes(saltedPw)));
                string clPw = inc.ReadString();
                if (clPw != saltedPw)
                {
                    unauthClient.failedAttempts++;
                    if (unauthClient.failedAttempts > 3)
                    {
                        //disconnect and ban after too many failed attempts
                        banList.BanPlayer("PASSWORDBAN", unauthClient.Connection.RemoteEndPoint.Address.ToString(), "Too many failed login attempts.", null);
                        DisconnectUnauthClient(inc, unauthClient, "Too many failed login attempts. You have been automatically banned from the server.");

                        Log(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " has been banned from the server (too many wrong passwords)", ServerLog.MessageType.Error);
                        DebugConsole.NewMessage(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " has been banned from the server (too many wrong passwords)", Color.Red);
                        GameServer.LogToClientconsole(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " has been banned from the server (too many wrong passwords)");
                        return;
                    }
                    else
                    {
                        //not disconnecting the player here, because they'll still use the same connection and nonce if they try logging in again
                        NetOutgoingMessage reject = server.CreateMessage();
                        reject.Write((byte)ServerPacketHeader.AUTH_FAILURE);
                        reject.Write("Wrong password! You have " + Convert.ToString(4 - unauthClient.failedAttempts) + " more attempts before you're banned from the server.");
                        Log(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " failed to join the server (incorrect password)", ServerLog.MessageType.Error);
                        DebugConsole.NewMessage(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " failed to join the server (incorrect password)", Color.Red);
                        GameServer.LogToClientconsole(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " failed to join the server (incorrect password)");
                        server.SendMessage(reject, unauthClient.Connection, NetDeliveryMethod.Unreliable);
                        unauthClient.AuthTimer = 10.0f;
                        return;
                    }
                }
            }
            string clVersion = inc.ReadString();
            string clPackageName = inc.ReadString();
            string clPackageHash = inc.ReadString();

            string clName = Client.SanitizeName(inc.ReadString());
            if (string.IsNullOrWhiteSpace(clName))
            {
                DisconnectUnauthClient(inc, unauthClient, "You need a name.");

                Log(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " couldn't join the server (no name given)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " couldn't join the server (no name given)", Color.Red);
                GameServer.LogToClientconsole(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " couldn't join the server (no name given)");
                return;
            }

            if (clVersion != GameMain.Version.ToString())
            {
                DisconnectUnauthClient(inc, unauthClient, "Version " + GameMain.Version + " required to connect to the server (Your version: " + clVersion + ")");

                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong game version) - CLVersion: " + clVersion + " vs servers " + GameMain.Version, ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong game version) - CLVersion: " + clVersion + " vs servers " + GameMain.Version, Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong game version) - CLVersion: " + clVersion + " vs servers " + GameMain.Version);
                return;
            }
            if (clPackageName != GameMain.SelectedPackage.Name)
            {
                DisconnectUnauthClient(inc, unauthClient, "Your content package (" + clPackageName + ") doesn't match the server's version (" + GameMain.SelectedPackage.Name + ")");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server with Incorrect Content Package: (" + clPackageName + ") vs servers (" + GameMain.SelectedPackage.Name + ")", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server with Incorrect Content Package: (" + clPackageName + ") vs servers (" + GameMain.SelectedPackage.Name + ")", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server with Incorrect Content Package: (" + clPackageName + ") vs servers (" + GameMain.SelectedPackage.Name + ")");
                return;
            }

            if (clPackageHash.Substring(0, 7).Contains("NILMOD_"))
            {
                if (!GameMain.NilMod.AllowNilModClients)
                {
                    DisconnectUnauthClient(inc, unauthClient, "This server does not permit Nilmod clients (Please rejoin using Vanilla).");
                    Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Nilmod clients are not permitted to connect).", ServerLog.MessageType.Connection);
                    DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Nilmod clients are not permitted to connect).", Color.Red);
                    GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Nilmod clients are not permitted to connect).");
                    return;
                }
                else
                {
                    isNilModClient = true;
                    clPackageHash = clPackageHash.Substring(7);
                }
            }
            else
            {
                if (!GameMain.NilMod.AllowVanillaClients)
                {
                    DisconnectUnauthClient(inc, unauthClient, "This server does not permit Vanilla clients (Please rejoin using Nilmod).");
                    Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Vanilla clients are not permitted to connect).", ServerLog.MessageType.Connection);
                    DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Vanilla clients are not permitted to connect).", Color.Red);
                    GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Vanilla clients are not permitted to connect).");
                    return;
                }
            }

            if (GameMain.NilMod.BypassMD5 == true)
            {
                if (clPackageHash != GameMain.NilMod.ServerMD5A && clPackageHash != GameMain.NilMod.ServerMD5B)
                {
                    DisconnectUnauthClient(inc, unauthClient, "Your content package (MD5: " + clPackageHash + ") doesn't match the server's version (MD5: " + GameMain.NilMod.ServerMD5A + ")");
                    Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong content package hash) MD5: (" + clPackageHash + ") vs servers MD5A: (" + GameMain.NilMod.ServerMD5A + "), MD5B: (" + GameMain.NilMod.ServerMD5B + ")", ServerLog.MessageType.Connection);
                    DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong content package hash):" + clPackageHash + " vs servers " + GameMain.NilMod.ServerMD5A + ", (" + GameMain.NilMod.ServerMD5B + ")", Color.Red);
                    GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong content package hash):" + clPackageHash + " vs servers " + GameMain.NilMod.ServerMD5A + ", (" + GameMain.NilMod.ServerMD5B + ")");
                    return;
                }
            }
            else
            {
                if (clPackageHash != GameMain.SelectedPackage.MD5hash.Hash)
                {
                    DisconnectUnauthClient(inc, unauthClient, "Your content package (MD5: " + clPackageHash + ") doesn't match the server's version (MD5: " + GameMain.SelectedPackage.MD5hash.Hash + ")");
                    Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong content package hash)", ServerLog.MessageType.Connection);
                    DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong content package hash)", Color.Red);
                    GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong content package hash)");
                    return;
                }
            }

            if (!whitelist.IsWhiteListed(clName, inc.SenderConnection.RemoteEndPoint.Address.ToString()))
            {
                DisconnectUnauthClient(inc, unauthClient, "You're not in this server's whitelist.");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (not in whitelist)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (not in whitelist)", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (not in whitelist)");
                return;
            }
            if (GameMain.NilMod.MinimumNameLength > clName.Length)
            {
                DisconnectUnauthClient(inc, unauthClient, "Your name is too short, Minimum length on this server is " + GameMain.NilMod.MinimumNameLength + " characters.");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name too short)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name too short, minimum length is " + GameMain.NilMod.MinimumNameLength + " characters)", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name too short, minimum length is " + GameMain.NilMod.MinimumNameLength + " characters)");
                return;
            }
            if (!Client.IsValidName(clName, this))
            {
                DisconnectUnauthClient(inc, unauthClient, "Your name contains illegal symbols.");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (invalid name)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (invalid name)", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (invalid name)");
                return;
            }

            if (!GameMain.NilMod.AllowCyrillicText && Regex.IsMatch(clName, @"\p{IsCyrillic}"))
            {
                DisconnectUnauthClient(inc, unauthClient, "This server does not allow Cyrillic alphabets.");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Name contains cyrillic characters)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Name contains cyrillic characters)", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Name contains cyrillic characters)");
                return;
            }

            if (!GameMain.NilMod.AllowEnglishText && Regex.IsMatch(clName, "^[a-zA-Z]*$"))
            {
                DisconnectUnauthClient(inc, unauthClient, "This server does not allow English alphabets.");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Name contains english characters)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Name contains english characters)", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Name contains english characters)");
                return;
            }

            //Nilmod prevent players rejoining as server hosts current name OR server name.
            if (Homoglyphs.Compare(clName.ToLower(), Name.ToLower()) || Homoglyphs.Compare(clName.ToLower(), GameMain.NilMod.PlayYourselfName.ToLower()) || Homoglyphs.Compare(clName.ToLower(), NilMod.NilModGriefWatcher.GriefWatchName))
            {
                DisconnectUnauthClient(inc, unauthClient, "That name is taken by the server.");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name taken by the server)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name taken by the server)", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name taken by the server)");
                return;
            }
            Client nameTaken = ConnectedClients.Find(c => Homoglyphs.Compare(c.Name.ToLower(), clName.ToLower()));
            if (nameTaken != null)
            {
                if (nameTaken.Connection.RemoteEndPoint.Address.ToString() == inc.SenderEndPoint.Address.ToString())
                {
                    //both name and IP address match, replace this player's connection
                    nameTaken.Connection.Disconnect("Your session was taken by a new connection on the same IP address.");
                    nameTaken.Connection = unauthClient.Connection;
                    nameTaken.InitClientSync(); //reinitialize sync ids because this is a new connection
                    unauthenticatedClients.Remove(unauthClient);
                    unauthClient = null;
                    return;
                }
                else
                {
                    //can't authorize this client
                    DisconnectUnauthClient(inc, unauthClient, "That name is taken.");
                    Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name already taken)", ServerLog.MessageType.Connection);
                    DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name already taken)", Color.Red);
                    GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name already taken)");
                    return;
                }
            }

            //MaxIdenticalIPConnections
            List<Client> SimilarIP = ConnectedClients.FindAll(c => c.Connection.RemoteEndPoint.Address.ToString() == inc.SenderConnection.RemoteEndPoint.Address.ToString());

            if (SimilarIP.Count >= GameMain.NilMod.MaxIdenticalIPConnections)
            {
                //can't authorize this client
                DisconnectUnauthClient(inc, unauthClient, "Too many similar IPs are on the server, max is " + GameMain.NilMod.MaxIdenticalIPConnections);
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Max Identical IP Connections exceeded)", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Max Identical IP Connections exceeded, max is " + GameMain.NilMod.MaxIdenticalIPConnections + ")", Color.Red);
                GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (Max Identical IP Connections exceeded, max is " + GameMain.NilMod.MaxIdenticalIPConnections + ")");
                return;
            }

            KickedClient kickedclient = null;

            if (GameMain.NilMod.KickedClients.Count > 0)
            {
                for (int i = GameMain.NilMod.KickedClients.Count - 1; i >= 0; i--)
                {
                    if (GameMain.NilMod.KickedClients[i].IPAddress == unauthClient.Connection.RemoteEndPoint.Address.ToString())
                    {
                        if (GameMain.NilMod.KickedClients[i].RejoinTimer > 0f)
                        {
                            DisconnectUnauthClient(inc, unauthClient, "You have been kicked for " + ToolBox.SecondsToReadableTime(GameMain.NilMod.KickedClients[i].RejoinTimer) + ".\n" + GameMain.NilMod.KickedClients[i].KickReason.Replace("You have been kicked from the server.", ""));
                            return;
                        }
                        else
                        {
                            kickedclient = GameMain.NilMod.KickedClients[i];
                        }

                    }
                }
            }

            if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
            {
                if (GameMain.NilMod.EnablePlayerLogSystem)
                {
                    DebugConsole.NewMessage("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + " - " + clName.ToString() + ")" + NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, false, true, false), Color.Red);
                    ServerLog.WriteLine("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + " - " + clName.ToString() + ")" + NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, false, true, false), ServerLog.MessageType.Connection);
                    GameServer.LogToClientconsole("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + " - " + clName.ToString() + ")" + NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, false, true, false));
                }
                else
                {
                    DebugConsole.NewMessage("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + " - " + clName.ToString() + ")", Color.Red);
                    ServerLog.WriteLine("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + " - " + clName.ToString() + ")", ServerLog.MessageType.Connection);
                    GameServer.LogToClientconsole("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + " - " + clName.ToString() + ")");
                }
                string banText = "";

                if (GameMain.NilMod.BansInfoAddBanName)
                {
                    banText = "You've been banned as '" + banList.GetBanName(inc.SenderEndPoint.Address.ToString()) + "'.";
                }
                else
                {
                    banText = "You've been banned from the server.";
                }

                //Add Ban duration text
                if (banList.GetBanExpiry(inc.SenderEndPoint.Address.ToString()) != null && GameMain.NilMod.BansInfoAddBanDuration)
                {
                    if (GameMain.NilMod.BansInfoUseRemainingTime)
                    {
                        TimeSpan banRemaining = Convert.ToDateTime(banList.GetBanExpiry(inc.SenderEndPoint.Address.ToString())).Subtract(DateTime.Now);

                        banText += "\n" + "Expires in: ";
                        if (banRemaining.Days > 0) banText += banRemaining.Days + " Days, ";
                        if (banRemaining.Hours > 0) banText += banRemaining.Hours + " Hours, ";
                        if (banRemaining.Minutes > 0) banText += banRemaining.Minutes + " Minutes, ";

                        banText = banText.Substring(0, banText.Length - 2);
                    }
                    else
                    {
                        banText += "\n" + "Expire on: " + banList.GetBanExpiry(inc.SenderEndPoint.Address.ToString())
                        + "\n" + "Currently: " + DateTime.Now.ToString();
                    }
                }
                //Permanent ban text
                else if (banList.GetBanExpiry(inc.SenderEndPoint.Address.ToString()) == null && GameMain.NilMod.BansInfoAddBanDuration)
                {
                    if (GameMain.NilMod.BansInfoAddBanName)
                    {
                        banText = "You've been permanently banned as '" + banList.GetBanName(inc.SenderEndPoint.Address.ToString()) + "'";
                    }
                    else
                    {
                        banText = "You've been permanently banned from the server.";
                    }
                }

                if (GameMain.NilMod.BansInfoAddCustomString)
                {
                    banText += "\n" + GameMain.NilMod.BansInfoCustomtext;
                }

                if (GameMain.NilMod.BansInfoAddBanReason)
                {
                    banText += "\n" + "for:" + banList.GetBanReason(inc.SenderEndPoint.Address.ToString());
                }

                DisconnectUnauthClient(inc, unauthClient, banText);

                /*
                    if (banList.GetBanExpiry(inc.SenderEndPoint.Address.ToString()) != null)
                {
                    //None Permament Ban
                    DisconnectUnauthClient(inc, unauthClient, "You have been banned As '" + banList.GetBanName(inc.SenderEndPoint.Address.ToString()) 
                        + "' with reason: " + banList.GetBanReason(inc.SenderEndPoint.Address.ToString())
                        + " \n Expires On: " + banList.GetBanExpiry(inc.SenderEndPoint.Address.ToString())
                        + " (Currently: " + DateTime.Now.ToString()
                        + "). \n" + "Appeal at www.barotraumaserver.com");
                }
                else
                {
                    //Permanent ban
                    DisconnectUnauthClient(inc, unauthClient, "You have been banned As '" + banList.GetBanName(inc.SenderEndPoint.Address.ToString()) + "' with reason: " + banList.GetBanReason(inc.SenderEndPoint.Address.ToString()) + "\n" + " \n This ban is permanent. \n" + "Appeal at Blabla (Custom text here)! \n \n \n \n \n \n.");
                }
                */
                return;
            }

            //new client
            Client newClient = new Client(clName, GetNewClientID());
            newClient.IsNilModClient = isNilModClient;
            newClient.RequiresNilModSync = isNilModClient;
            newClient.NilModSyncResendTimer = 4f;
            newClient.InitClientSync();
            newClient.Connection = unauthClient.Connection;
            unauthenticatedClients.Remove(unauthClient);
            unauthClient = null;

            SavedClientPermission savedPermissions = clientPermissions.Find(cp => cp.IP == newClient.Connection.RemoteEndPoint.Address.ToString());
            if (savedPermissions == null) savedPermissions = defaultpermission;

            newClient.OwnerSlot = savedPermissions.OwnerSlot;
            newClient.AdministratorSlot = savedPermissions.AdministratorSlot;
            newClient.TrustedSlot = savedPermissions.TrustedSlot;

            newClient.AllowInGamePM = savedPermissions.AllowInGamePM;
            newClient.GlobalChatSend = savedPermissions.GlobalChatSend;
            newClient.GlobalChatReceive = savedPermissions.GlobalChatReceive;
            newClient.KarmaImmunity = savedPermissions.KarmaImmunity;
            newClient.BypassSkillRequirements = savedPermissions.BypassSkillRequirements;
            newClient.PrioritizeJob = savedPermissions.PrioritizeJob;
            newClient.IgnoreJobMinimum = savedPermissions.IgnoreJobMinimum;
            newClient.KickImmunity = savedPermissions.KickImmunity;
            newClient.BanImmunity = savedPermissions.BanImmunity;

            newClient.HideJoin = savedPermissions.HideJoin;
            newClient.AccessDeathChatAlive = savedPermissions.AccessDeathChatAlive;
            newClient.AdminPrivateMessage = savedPermissions.AdminPrivateMessage;
            newClient.AdminChannelSend = savedPermissions.AdminChannelSend;
            newClient.AdminChannelReceive = savedPermissions.AdminChannelReceive;
            newClient.SendServerConsoleInfo = savedPermissions.SendServerConsoleInfo;
            newClient.CanBroadcast = savedPermissions.CanBroadcast;

            newClient.SetPermissions(savedPermissions.Permissions, savedPermissions.PermittedCommands);

            ConnectedClients.Add(newClient);

#if CLIENT
            GameSession.inGameInfo.AddClient(newClient);
            GameMain.NetLobbyScreen.AddPlayer(newClient.Name);
#endif
            if (GameMain.NilMod.EnableVPNBanlist)
            {
                CoroutineManager.StartCoroutine(NilMod.NilModVPNBanlist.CheckVPNBan(newClient, clName.ToString()), "NilModVPNBanlist");
            }

            if (!GameMain.NilMod.EnableVPNBanlist)
            {
                if (GameMain.NilMod.EnablePlayerLogSystem)
                {
                    if (!newClient.HideJoin) GameMain.Server.SendChatMessage(NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, false, false, true), ChatMessageType.Server, null);
                    DebugConsole.NewMessage(NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, true, true, true), Color.White);
                    Log(NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, true, true, true), ServerLog.MessageType.Connection);
                    GameServer.LogToClientconsole(NilMod.NilModPlayerLog.ListPrevious(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName, true, true, true));

                    NilMod.NilModPlayerLog.LogPlayer(inc.SenderConnection.RemoteEndPoint.Address.ToString(), clName);
                }
                else
                {
                    DisconnectedCharacter ReconnectedClient = null;

                    if (GameMain.NilMod.DisconnectedCharacters.Count > 0)
                    {
                        ReconnectedClient = GameMain.NilMod.DisconnectedCharacters.Find(dc => dc.IPAddress == inc.SenderConnection.RemoteEndPoint.Address.ToString() && dc.clientname == clName);
                    }

                    if (kickedclient != null)
                    {
                        GameMain.Server.SendChatMessage("Recently Kicked Player " + clName + " (" + kickedclient.clientname + ") has rejoined the server.", ChatMessageType.Server, null);
                        DebugConsole.NewMessage("Recently Kicked Player " + clName + " (" + kickedclient.clientname + ") (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has joined the server.", Color.White);
                        Log("Recently Kicked Player " + clName + " (" + kickedclient.clientname + ") (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has joined the server.", ServerLog.MessageType.Connection);
                        GameServer.LogToClientconsole("Recently Kicked Player " + clName + " (" + kickedclient.clientname + ") (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has joined the server.");

                        if (GameMain.NilMod.ClearKickStateNameOnRejoin)
                        {
                            GameMain.NilMod.KickedClients.Remove(kickedclient);
                        }
                        else
                        {
                            kickedclient.ExpireTimer += GameMain.NilMod.KickStateNameTimerIncreaseOnRejoin;
                            if (kickedclient.ExpireTimer > GameMain.NilMod.KickMaxStateNameTimer) kickedclient.ExpireTimer = GameMain.NilMod.KickMaxStateNameTimer;
                        }
                    }
                    else if (ReconnectedClient == null)
                    {
                        if(!newClient.HideJoin) GameMain.Server.SendChatMessage(clName + " has joined the server.", ChatMessageType.Server, null);
                        DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has joined the server.", Color.White);
                        Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has joined the server.", ServerLog.MessageType.Connection);
                        GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has joined the server.");
                    }
                    else
                    {
                        if (!newClient.HideJoin) GameMain.Server.SendChatMessage(clName + " has reconnected to the server.", ChatMessageType.Server, null);
                        DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has reconnected to the server.", Color.White);
                        Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has reconnected to the server.", ServerLog.MessageType.Connection);
                        GameServer.LogToClientconsole(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") has reconnected to the server.");
                    }
                }
            }
        }

        private void DisconnectUnauthClient(NetIncomingMessage inc, UnauthenticatedClient unauthClient, string reason)
        {
            inc.SenderConnection.Disconnect(reason);

            if (unauthClient != null)
            {
                unauthenticatedClients.Remove(unauthClient);
            }
        }
    }
}
