﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma.Networking
{
    partial class RespawnManager : Entity, IServerSerializable
    {
        private List<Client> GetClientsToRespawn()
        {
            GameServer server = networkMember as GameServer;

            return networkMember.ConnectedClients.FindAll(c =>
                c.InGame &&
                (!c.SpectateOnly || (!server.ServerSettings.AllowSpectating && server.OwnerConnection != c.Connection)) &&
                (c.Character == null || c.Character.IsDead));
        }

        private List<CharacterInfo> GetBotsToRespawn()
        {
            GameServer server = networkMember as GameServer;

            if (server.ServerSettings.BotSpawnMode == BotSpawnMode.Normal)
            {
                return Character.CharacterList
                    .FindAll(c => c.TeamID == Character.TeamType.Team1 && c.AIController != null && c.Info != null && c.IsDead)
                    .Select(c => c.Info)
                    .ToList();
            }

            int currPlayerCount = server.ConnectedClients.Count(c => 
                c.InGame && 
                (!c.SpectateOnly || (!server.ServerSettings.AllowSpectating && server.OwnerConnection != c.Connection)));

            var existingBots = Character.CharacterList
                .FindAll(c => c.TeamID == Character.TeamType.Team1 && c.AIController != null && c.Info != null);

            int requiredBots = server.ServerSettings.BotCount - currPlayerCount;
            requiredBots -= existingBots.Count(b => !b.IsDead);

            List<CharacterInfo> botsToRespawn = new List<CharacterInfo>();
            for (int i = 0; i < requiredBots; i++)
            {
                CharacterInfo botToRespawn = existingBots.Find(b => b.IsDead)?.Info;
                if (botToRespawn == null)
                {
                    botToRespawn = new CharacterInfo(Character.HumanConfigFile);
                }
                else
                {
                    existingBots.Remove(botToRespawn.Character);
                }
                botsToRespawn.Add(botToRespawn);
            }
            return botsToRespawn;
        }

        partial void UpdateWaiting(float deltaTime)
        {
            var server = networkMember as GameServer;

            int characterToRespawnCount = GetClientsToRespawn().Count;
            int totalCharacterCount = server.ConnectedClients.Count;
            /*if (server.Character != null)
            {
                totalCharacterCount++;
                if (server.Character.IsDead) characterToRespawnCount++;
            }*/
            bool startCountdown = (float)characterToRespawnCount >= Math.Max((float)totalCharacterCount * server.ServerSettings.MinRespawnRatio, 1.0f);

            if (startCountdown != CountdownStarted)
            {
                CountdownStarted = startCountdown;
                server.CreateEntityEvent(this);                
            }

            if (!CountdownStarted) return;

            respawnTimer -= deltaTime;
            if (respawnTimer <= 0.0f)
            {
                respawnTimer = server.ServerSettings.RespawnInterval;

                DispatchShuttle();
            }

            if (respawnShuttle == null) return;

            respawnShuttle.Velocity = Vector2.Zero;

            if (shuttleSteering != null)
            {
                shuttleSteering.AutoPilot = false;
                shuttleSteering.MaintainPos = false;
            }
        }

        partial void DispatchShuttle()
        {
            var server = networkMember as GameServer;
            if (server == null) return;

            if (respawnShuttle != null)
            {
                state = State.Transporting;
                server.CreateEntityEvent(this);

                ResetShuttle();

                if (shuttleSteering != null)
                {
                    shuttleSteering.TargetVelocity = Vector2.Zero;
                }

                GameServer.Log("Dispatching the respawn shuttle.", ServerLog.MessageType.Spawning);

                RespawnCharacters();

                CoroutineManager.StopCoroutines("forcepos");
                CoroutineManager.StartCoroutine(ForceShuttleToPos(Level.Loaded.StartPosition - Vector2.UnitY * Level.ShaftHeight, 100.0f), "forcepos");
            }
            else
            {
                state = State.Waiting;
                GameServer.Log("Respawning everyone in main sub.", ServerLog.MessageType.Spawning);
                server.CreateEntityEvent(this);

                RespawnCharacters();
            }
        }

        partial void RespawnCharactersProjSpecific()
        {
            var server = networkMember as GameServer;
            if (server == null) return;

            var respawnSub = respawnShuttle ?? Submarine.MainSub;

            var clients = GetClientsToRespawn();
            foreach (Client c in clients)
            {
                //all characters are in Team 1 in game modes/missions with only one team.
                //if at some point we add a game mode with multiple teams where respawning is possible, this needs to be reworked
                c.TeamID = Character.TeamType.Team1;
                if (c.CharacterInfo == null) c.CharacterInfo = new CharacterInfo(Character.HumanConfigFile, c.Name);
            }
            List<CharacterInfo> characterInfos = clients.Select(c => c.CharacterInfo).ToList();

            var botsToSpawn = GetBotsToRespawn();
            characterInfos.AddRange(botsToSpawn);
            
            server.AssignJobs(clients);
            foreach (Client c in clients)
            {
                c.CharacterInfo.Job = new Job(c.AssignedJob);
            }

            //the spawnpoints where the characters will spawn
            var shuttleSpawnPoints = WayPoint.SelectCrewSpawnPoints(characterInfos, respawnSub);
            //the spawnpoints where they would spawn if they were spawned inside the main sub
            //(in order to give them appropriate ID card tags)
            var mainSubSpawnPoints = WayPoint.SelectCrewSpawnPoints(characterInfos, Submarine.MainSub);

            ItemPrefab divingSuitPrefab = MapEntityPrefab.Find(null, "divingsuit") as ItemPrefab;
            ItemPrefab oxyPrefab = MapEntityPrefab.Find(null, "oxygentank") as ItemPrefab;
            ItemPrefab scooterPrefab = MapEntityPrefab.Find(null, "underwaterscooter") as ItemPrefab;
            ItemPrefab batteryPrefab = MapEntityPrefab.Find(null, "batterycell") as ItemPrefab;

            var cargoSp = WayPoint.WayPointList.Find(wp => wp.Submarine == respawnSub && wp.SpawnType == SpawnType.Cargo);

            for (int i = 0; i < characterInfos.Count; i++)
            {
                bool bot = i >= clients.Count;

                var character = Character.Create(characterInfos[i], shuttleSpawnPoints[i].WorldPosition, characterInfos[i].Name, !bot, bot);
                character.TeamID = Character.TeamType.Team1;
                
                if (bot)
                {
                    GameServer.Log(string.Format("Respawning bot {0} as {1}", character.Info.Name, characterInfos[i].Job.Name), ServerLog.MessageType.Spawning);
                }
                else
                {
                    //tell the respawning client they're no longer a traitor
                    if (GameMain.Server.TraitorManager != null && clients[i].Character != null)
                    {
                        if (GameMain.Server.TraitorManager.TraitorList.Any(t => t.Character == clients[i].Character))
                        {
                            GameMain.Server.SendDirectChatMessage(TextManager.Get("traitorrespawnmessage"), clients[i], ChatMessageType.MessageBox);
                        }
                    }

                    clients[i].Character = character;
                    character.OwnerClientIP = clients[i].Connection.RemoteEndPoint.Address.ToString();
                    character.OwnerClientName = clients[i].Name;
                    GameServer.Log(string.Format("Respawning {0} ({1}) as {2}", clients[i].Name, clients[i].Connection?.RemoteEndPoint?.Address, characterInfos[i].Job.Name), ServerLog.MessageType.Spawning);
                }

                if (divingSuitPrefab != null && oxyPrefab != null && respawnShuttle != null)
                {
                    Vector2 pos = cargoSp == null ? character.Position : cargoSp.Position;
                    if (divingSuitPrefab != null && oxyPrefab != null)
                    {
                        var divingSuit = new Item(divingSuitPrefab, pos, respawnSub);
                        Spawner.CreateNetworkEvent(divingSuit, false);
                        respawnItems.Add(divingSuit);

                        var oxyTank = new Item(oxyPrefab, pos, respawnSub);
                        Spawner.CreateNetworkEvent(oxyTank, false);
                        divingSuit.Combine(oxyTank);
                        respawnItems.Add(oxyTank);
                    }

                    if (scooterPrefab != null && batteryPrefab != null)
                    {
                        var scooter = new Item(scooterPrefab, pos, respawnSub);
                        Spawner.CreateNetworkEvent(scooter, false);

                        var battery = new Item(batteryPrefab, pos, respawnSub);
                        Spawner.CreateNetworkEvent(battery, false);

                        scooter.Combine(battery);
                        respawnItems.Add(scooter);
                        respawnItems.Add(battery);
                    }
                }

                //give the character the items they would've gotten if they had spawned in the main sub
                character.GiveJobItems(mainSubSpawnPoints[i]);

                //add the ID card tags they should've gotten when spawning in the shuttle
                foreach (Item item in character.Inventory.Items)
                {
                    if (item == null || item.Prefab.Identifier != "idcard") continue;
                    foreach (string s in shuttleSpawnPoints[i].IdCardTags)
                    {
                        item.AddTag(s);
                    }
                    if (!string.IsNullOrWhiteSpace(shuttleSpawnPoints[i].IdCardDesc))
                        item.Description = shuttleSpawnPoints[i].IdCardDesc;
                }
            }

        }
    }
}
