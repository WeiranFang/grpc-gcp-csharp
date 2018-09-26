﻿using Google.Apis.Auth.OAuth2;
using Google.Cloud.Spanner.V1;
using Google.Protobuf;
using Grpc.Auth;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Grpc.Gcp.IntegrationTest
{
    [TestClass]
    public class SpannerTest
    {
        private const string Target = "spanner.googleapis.com";
        private const string DatabaseUrl = "projects/grpc-gcp/instances/sample/databases/benchmark";
        private const string TableName = "storage";
        private const string ColumnId = "payload";
        private const Int32 DefaultMaxChannelsPerTarget = 10;

        private ApiConfig config = new ApiConfig();
        private GcpCallInvoker invoker;
        private Spanner.SpannerClient client;

        [TestInitialize]
        public void SetUp()
        {
            //InitApiConfig(1, 10);
            InitApiConfigFromFile();
            InitClient();
        }

        private void InitClient()
        {
            GoogleCredential credential = GoogleCredential.GetApplicationDefault();
            IList<ChannelOption> options = new List<ChannelOption>() {
                new ChannelOption(GcpCallInvoker.ApiConfigChannelArg, config.ToString()) };
            invoker = new GcpCallInvoker(Target, credential.ToChannelCredentials(), options);
            client = new Spanner.SpannerClient(invoker);
        }

        private void InitApiConfigFromFile()
        {
            MessageParser<ApiConfig> parser = ApiConfig.Parser;
            string text = System.IO.File.ReadAllText(@"spanner.grpc.config");
            config = parser.ParseJson(text);
        }

        private void InitApiConfig(int maxConcurrentStreams, int maxSize)
        {
            config.ChannelPool = new ChannelPoolConfig
            {
                MaxConcurrentStreamsLowWatermark = (uint)maxConcurrentStreams,
                MaxSize = (uint)maxSize
            };
            AddMethod(config, "/google.spanner.v1.Spanner/CreateSession", AffinityConfig.Types.Command.Bind, "name");
            AddMethod(config, "/google.spanner.v1.Spanner/GetSession", AffinityConfig.Types.Command.Bound, "name");
            AddMethod(config, "/google.spanner.v1.Spanner/DeleteSession", AffinityConfig.Types.Command.Unbind, "name");
            AddMethod(config, "/google.spanner.v1.Spanner/ExecuteSql", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/ExecuteStreamingSql", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/Read", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/StreamingRead", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/BeginTransaction", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/Commit", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/Rollback", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/PartitionQuery", AffinityConfig.Types.Command.Bound, "session");
            AddMethod(config, "/google.spanner.v1.Spanner/PartitionRead", AffinityConfig.Types.Command.Bound, "session");
        }

        private void AddMethod(ApiConfig config, string name, AffinityConfig.Types.Command command, string affinityKey)
        {
            MethodConfig method = new MethodConfig();
            method.Name.Add(name);
            method.Affinity = new AffinityConfig
            {
                Command = command,
                AffinityKey = affinityKey
            };
            config.Method.Add(method);
        }

        [TestMethod]
        public void CreateSessionWithNewChannel()
        {
            IList<AsyncUnaryCall<Session>> calls = new List<AsyncUnaryCall<Session>>();

            for (int i = 0; i < DefaultMaxChannelsPerTarget; i++)
            {
                var call = client.CreateSessionAsync(
                    new CreateSessionRequest { Database = DatabaseUrl });
                calls.Add(call);
                Assert.AreEqual(i + 1, invoker.GetChannelRefsForTest().Count);
            }
            for (int i = 0; i < calls.Count; i++)
            {
                client.DeleteSession(
                    new DeleteSessionRequest { Name = calls[i].ResponseAsync.Result.Name });
            }

            calls.Clear();

            for (int i = 0; i < DefaultMaxChannelsPerTarget; i++)
            {
                var call = client.CreateSessionAsync(
                    new CreateSessionRequest { Database = DatabaseUrl });
                calls.Add(call);
                Assert.AreEqual(DefaultMaxChannelsPerTarget, invoker.GetChannelRefsForTest().Count);
            }
            for (int i = 0; i < calls.Count; i++)
            {
                client.DeleteSession(
                    new DeleteSessionRequest { Name = calls[i].ResponseAsync.Result.Name });
            }
        }

        [TestMethod]
        public void CreateSessionWithReusedChannel()
        {
            for (int i = 0; i < DefaultMaxChannelsPerTarget * 2; i++)
            {
                Session session;
                session = client.CreateSession(
                    new CreateSessionRequest { Database = DatabaseUrl });

                Assert.IsNotNull(session);
                Assert.AreEqual(1, invoker.GetChannelRefsForTest().Count);

                client.DeleteSession(new DeleteSessionRequest { Name = session.Name });
            }
        }

        [TestMethod]
        public void CreateListDeleteSession()
        {
            Session session;
            {
                CreateSessionRequest request = new CreateSessionRequest
                {
                    Database = DatabaseUrl
                };
                session = client.CreateSession(request);
                Assert.IsNotNull(session);
                AssertAffinityCount(1);
            }

            {
                ListSessionsRequest request = new ListSessionsRequest
                {
                    Database = DatabaseUrl
                };
                ListSessionsResponse response = client.ListSessions(request);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Sessions);
                Assert.IsTrue(response.Sessions.Any(item => item.Name == session.Name));
                AssertAffinityCount(1);
            }

            {
                DeleteSessionRequest request = new DeleteSessionRequest
                {
                    Name = session.Name
                };
                client.DeleteSession(request);
                AssertAffinityCount(0);
            }

            {
                ListSessionsRequest request = new ListSessionsRequest
                {
                    Database = DatabaseUrl
                };
                ListSessionsResponse response = client.ListSessions(request);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Sessions);
                Assert.IsFalse(response.Sessions.Any(item => item.Name == session.Name));
                AssertAffinityCount(0);
            }

        }

        [TestMethod]
        public void ExecuteSql()
        {
            Session session;
            {
                CreateSessionRequest request = new CreateSessionRequest
                {
                    Database = DatabaseUrl
                };
                session = client.CreateSession(request);
                Assert.IsNotNull(session);
                AssertAffinityCount(1);
            }
            {
                ExecuteSqlRequest request = new ExecuteSqlRequest
                {
                    Session = session.Name,
                    Sql = string.Format("select id, data from {0}", TableName)
                };
                ResultSet resultSet = client.ExecuteSql(request);
                AssertAffinityCount(1);
                Assert.IsNotNull(resultSet);
                Assert.AreEqual(1, resultSet.Rows.Count);
                Assert.AreEqual(ColumnId, resultSet.Rows[0].Values[0].StringValue);
            }
            {
                DeleteSessionRequest request = new DeleteSessionRequest
                {
                    Name = session.Name
                };
                client.DeleteSession(request);
                AssertAffinityCount(0);
            }
        }

        [TestMethod]
        public void ExecuteStreamingSql()
        {
            Session session;

            session = client.CreateSession(
                new CreateSessionRequest { Database = DatabaseUrl });
            Assert.IsNotNull(session);
            AssertAffinityCount(1);

            var streamingCall = client.ExecuteStreamingSql(
                new ExecuteSqlRequest
                {
                    Session = session.Name,
                    Sql = string.Format("select id, data from {0}", TableName)
                });

            AssertAffinityCount(1, expectedActiveStreamCount: 1);

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            var responseStream = streamingCall.ResponseStream;
            PartialResultSet firstResultSet = null;
            while (responseStream.MoveNext(token).Result)
            {
                if (firstResultSet == null) firstResultSet = responseStream.Current;
            }
            Assert.AreEqual(ColumnId, firstResultSet?.Values[0].StringValue);
            AssertAffinityCount(1);

            client.DeleteSession(new DeleteSessionRequest { Name = session.Name });
            AssertAffinityCount(0);
        }

        [TestMethod]
        public void ExecuteSqlAsync()
        {
            Session session;

            session = client.CreateSession(
                new CreateSessionRequest { Database = DatabaseUrl });
            Assert.IsNotNull(session);
            AssertAffinityCount(1);

            AsyncUnaryCall<ResultSet> call = client.ExecuteSqlAsync(
                new ExecuteSqlRequest
                {
                    Session = session.Name,
                    Sql = string.Format("select id, data from {0}", TableName)
                });
            AssertAffinityCount(1, expectedActiveStreamCount: 1);

            ResultSet resultSet = call.ResponseAsync.Result;

            AssertAffinityCount(1);

            Assert.IsNotNull(resultSet);
            Assert.AreEqual(1, resultSet.Rows.Count);
            Assert.AreEqual(ColumnId, resultSet.Rows[0].Values[0].StringValue);

            client.DeleteSession(new DeleteSessionRequest { Name = session.Name });
            AssertAffinityCount(0);
        }

        [TestMethod]
        public void BoundUnbindInvalidAffinityKey()
        {
            GetSessionRequest getSessionRequest = new GetSessionRequest
            {
                Name = "random_name"
            };
            Assert.ThrowsException<RpcException>(() => client.GetSession(getSessionRequest));

            DeleteSessionRequest deleteSessionRequest = new DeleteSessionRequest
            {
                Name = "random_name"
            };

            Assert.ThrowsException<RpcException>(() => client.DeleteSession(deleteSessionRequest));
        }

        [TestMethod]
        public void BoundAfterUnbind()
        {
            CreateSessionRequest request = new CreateSessionRequest
            {
                Database = DatabaseUrl
            };
            Session session = client.CreateSession(request);

            Assert.AreEqual(1, invoker.GetChannelRefsByAffinityKeyForTest().Count);

            DeleteSessionRequest deleteSessionRequest = new DeleteSessionRequest
            {
                Name = session.Name
            };
            client.DeleteSession(deleteSessionRequest);

            Assert.AreEqual(0, invoker.GetChannelRefsByAffinityKeyForTest().Count);

            GetSessionRequest getSessionRequest = new GetSessionRequest();
            getSessionRequest.Name = session.Name;
            Assert.ThrowsException<Grpc.Core.RpcException>(() => client.GetSession(getSessionRequest));

        }

        [TestMethod]
        public void ConcurrentStreams()
        {
            config = new ApiConfig();
            int lowWatermark = 5;
            InitApiConfig(lowWatermark, 10);
            InitClient();

            var sessions = new List<Session>();
            var calls = new List<AsyncServerStreamingCall<PartialResultSet>>();

            for (int i = 0; i < lowWatermark; i++)
            {
                Session session = client.CreateSession(
                    new CreateSessionRequest { Database = DatabaseUrl });
                AssertAffinityCount(i + 1, expectedActiveStreamCount: i);
                Assert.IsNotNull(session);

                sessions.Add(session);

                var streamingCall = client.ExecuteStreamingSql(
                    new ExecuteSqlRequest
                    {
                        Session = session.Name,
                        Sql = string.Format("select id, data from {0}", TableName)
                    });
                AssertAffinityCount(i + 1, expectedActiveStreamCount: i + 1);
                calls.Add(streamingCall);
            }

            // When number of active streams reaches the lowWaterMark,
            // New channel should be created.

            Session anotherSession = client.CreateSession(
                new CreateSessionRequest { Database = DatabaseUrl });
            var channelRefs = invoker.GetChannelRefsForTest();
            Assert.AreEqual(2, channelRefs.Count);
            Assert.AreEqual(lowWatermark, channelRefs[0].AffinityCount);
            Assert.AreEqual(lowWatermark, channelRefs[0].ActiveStreamCount);
            Assert.AreEqual(1, channelRefs[1].AffinityCount);
            Assert.AreEqual(0, channelRefs[1].ActiveStreamCount);
            Assert.IsNotNull(anotherSession);

            sessions.Add(anotherSession);

            var anotherStreamingCall = client.ExecuteStreamingSql(
                new ExecuteSqlRequest
                {
                    Session = anotherSession.Name,
                    Sql = string.Format("select id, data from {0}", TableName)
                });
            channelRefs = invoker.GetChannelRefsForTest();
            Assert.AreEqual(2, channelRefs.Count);
            Assert.AreEqual(lowWatermark, channelRefs[0].AffinityCount);
            Assert.AreEqual(lowWatermark, channelRefs[0].ActiveStreamCount);
            Assert.AreEqual(1, channelRefs[1].AffinityCount);
            Assert.AreEqual(1, channelRefs[1].ActiveStreamCount);

            calls.Add(anotherStreamingCall);

            // Clean open streams.
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            for (int i = 0; i < calls.Count; i++)
            {
                var responseStream = calls[i].ResponseStream;
                while (responseStream.MoveNext(token).Result) { };
            }
            channelRefs = invoker.GetChannelRefsForTest();
            Assert.AreEqual(2, channelRefs.Count);
            Assert.AreEqual(lowWatermark, channelRefs[0].AffinityCount);
            Assert.AreEqual(0, channelRefs[0].ActiveStreamCount);
            Assert.AreEqual(1, channelRefs[1].AffinityCount);
            Assert.AreEqual(0, channelRefs[1].ActiveStreamCount);

            // Delete all sessions to clean affinity.
            for (int i = 0; i < sessions.Count; i++)
            {
                client.DeleteSession(new DeleteSessionRequest { Name = sessions[i].Name });
            }
            channelRefs = invoker.GetChannelRefsForTest();
            Assert.AreEqual(2, channelRefs.Count);
            Assert.AreEqual(0, channelRefs[0].AffinityCount);
            Assert.AreEqual(0, channelRefs[0].ActiveStreamCount);
            Assert.AreEqual(0, channelRefs[1].AffinityCount);
            Assert.AreEqual(0, channelRefs[1].ActiveStreamCount);
        }

        [TestMethod]
        public void ShutdownChannels()
        {
            IList<AsyncUnaryCall<Session>> calls = new List<AsyncUnaryCall<Session>>();

            for (int i = 0; i < DefaultMaxChannelsPerTarget; i++)
            {
                var call = client.CreateSessionAsync(
                    new CreateSessionRequest { Database = DatabaseUrl });
                calls.Add(call);
                Assert.AreEqual(i + 1, invoker.GetChannelRefsForTest().Count);
            }
            for (int i = 0; i < calls.Count; i++)
            {
                client.DeleteSession(
                    new DeleteSessionRequest { Name = calls[i].ResponseAsync.Result.Name });
            }

            var channelRefs = invoker.GetChannelRefsForTest();
            for (int i = 0; i < channelRefs.Count; i++)
            {
                var channel = channelRefs[i].Channel;
                Assert.AreEqual(ChannelState.Ready, channel.State);
            }

            // Shutdown all channels in the channel pool.
            invoker.ShutdownAsync().Wait();

            for (int i = 0; i < channelRefs.Count; i++)
            {
                var channel = channelRefs[i].Channel;
                Assert.AreEqual(ChannelState.Shutdown, channel.State);
            }
        }

        private void AssertAffinityCount(int expectedAffinityCount, int expectedActiveStreamCount = 0)
        {
            var channelRefs = invoker.GetChannelRefsForTest();
            Assert.AreEqual(1, channelRefs.Count);
            Assert.AreEqual(expectedAffinityCount, channelRefs[0].AffinityCount);
            Assert.AreEqual(expectedActiveStreamCount, channelRefs[0].ActiveStreamCount);
        }
    }
}
