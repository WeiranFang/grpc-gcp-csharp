﻿using Google.Protobuf;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Gcp
{
    /// <summary>
    /// Invokes client RPCs using <see cref="Calls"/>.
    /// Calls are made through underlying gcp channel pool.
    /// </summary
    public class GcpCallInvoker : CallInvoker
    {
        private static int clientChannelIdCounter;

        public const string ApiConfigChannelArg = "grpc_gcp.api_config";
        private const string ClientChannelId = "grpc_gcp.client_channel.id";
        private const Int32 DefaultChannelPoolSize = 10;
        private const Int32 DefaultMaxCurrentStreams = 100;

        // Lock to protect the channel reference collections, as they're not thread-safe.
        private readonly Object thisLock = new Object();
        private readonly IDictionary<string, ChannelRef> channelRefByAffinityKey = new Dictionary<string, ChannelRef>();
        private readonly IList<ChannelRef> channelRefs = new List<ChannelRef>();

        // Access to these fields does not need to be protected by the lock: the objects are never modified.
        private readonly string target;
        private readonly ApiConfig apiConfig;
        private readonly IDictionary<string, AffinityConfig> affinityByMethod;
        private readonly ChannelCredentials credentials;
        private readonly IEnumerable<ChannelOption> options;

        /// <summary>
        /// Initializes a new instance of the <see cref="Grpc.Gcp.GcpCallInvoker"/> class.
        /// </summary>
        /// <param name="target">Target of the underlying grpc channels.</param>
        /// <param name="credentials">Credentials to secure the underlying grpc channels.</param>
        /// <param name="options">Channel options to be used by the underlying grpc channels.</param>
        public GcpCallInvoker(string target, ChannelCredentials credentials, IEnumerable<ChannelOption> options = null)
        {
            this.target = target;
            this.credentials = credentials;
            this.apiConfig = InitDefaultApiConfig();

            if (options != null)
            {
                ChannelOption option = options.FirstOrDefault(opt => opt.Name == ApiConfigChannelArg);
                if (option != null)
                {
                    try
                    {
                        apiConfig = ApiConfig.Parser.ParseJson(option.StringValue);
                    }
                    catch (Exception ex)
                    {
                        if (ex is InvalidOperationException || ex is InvalidJsonException || ex is InvalidProtocolBufferException)
                        {
                            throw new ArgumentException("Invalid API config!", ex);
                        }
                        throw;
                    }
                    if (apiConfig.ChannelPool == null)
                    {
                        throw new ArgumentException("Invalid API config: no channel pool settings");
                    }
                }
                this.options = options.Where(o => o.Name != ApiConfigChannelArg).ToList();
            }
            else
            {
                this.options = Enumerable.Empty<ChannelOption>();
            }

            affinityByMethod = InitAffinityByMethodIndex(apiConfig);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Grpc.Gcp.GcpCallInvoker"/> class.
        /// </summary>
        /// <param name="host">Hostname of target.</param>
        /// <param name="port">Port number of target</param>
        /// <param name="credentials">Credentials to secure the underlying grpc channels.</param>
        /// <param name="options">Channel options to be used by the underlying grpc channels.</param>
        public GcpCallInvoker(string host, int port, ChannelCredentials credentials, IEnumerable<ChannelOption> options = null) :
            this($"{host}:{port}", credentials, options)
        { }

        private ApiConfig InitDefaultApiConfig() =>
            new ApiConfig
            {
                ChannelPool = new ChannelPoolConfig
                {
                    MaxConcurrentStreamsLowWatermark = (uint)DefaultMaxCurrentStreams,
                    MaxSize = (uint)DefaultChannelPoolSize
                }
            };

        private IDictionary<string, AffinityConfig> InitAffinityByMethodIndex(ApiConfig config)
        {
            IDictionary<string, AffinityConfig> index = new Dictionary<string, AffinityConfig>();
            if (config != null)
            {
                foreach (MethodConfig method in config.Method)
                {
                    // TODO(fengli): supports wildcard in method selector.
                    foreach (string name in method.Name)
                    {
                        index.Add(name, method.Affinity);
                    }
                }
            }
            return index;
        }

        private ChannelRef GetChannelRef(string affinityKey = null)
        {
            // TODO(fengli): Supports load reporting.
            lock (thisLock)
            {
                if (!string.IsNullOrEmpty(affinityKey))
                {
                    // Finds the gRPC channel according to the affinity key.
                    if (channelRefByAffinityKey.TryGetValue(affinityKey, out ChannelRef channelRef))
                    {
                        return channelRef;
                    }
                    // TODO(fengli): Affinity key not found, log an error.
                }

                // TODO(fengli): Creates new gRPC channels on demand, depends on the load reporting.
                IOrderedEnumerable<ChannelRef> orderedChannelRefs =
                    channelRefs.OrderBy(channelRef => channelRef.ActiveStreamCount);
                foreach (ChannelRef channelRef in orderedChannelRefs)
                {
                    if (channelRef.ActiveStreamCount < apiConfig.ChannelPool.MaxConcurrentStreamsLowWatermark)
                    {
                        // If there's a free channel, use it.
                        return channelRef;
                    }
                    else
                    {
                        //  If all channels are busy, break.
                        break;
                    }
                }
                int count = channelRefs.Count;
                if (count < apiConfig.ChannelPool.MaxSize)
                {
                    // Creates a new gRPC channel.
                    GrpcEnvironment.Logger.Info("Grpc.Gcp creating new channel");
                    Channel channel = new Channel(target, credentials,
                        options.Concat(new[] { new ChannelOption(ClientChannelId, Interlocked.Increment (ref clientChannelIdCounter)) }));
                    ChannelRef channelRef = new ChannelRef(channel, count);
                    channelRefs.Add(channelRef);
                    return channelRef;
                }
                // If all channels are overloaded and the channel pool is full already,
                // return the channel with least active streams.
                return orderedChannelRefs.First();
            }
        }

        private string GetAffinityKeyFromProto(string affinityKey, IMessage message)
        {
            if (string.IsNullOrEmpty(affinityKey))
            {
                return null;
            }
            string[] names = affinityKey.Split('.');
            foreach (string name in names)
            {
                var field = message.Descriptor.FindFieldByName(name);
                if (field == null)
                {
                    throw new InvalidOperationException($"Field {name} not present in message {message.Descriptor.Name}");
                }
                var accessor = field.Accessor;
                if (accessor == null)
                {
                    throw new InvalidOperationException($"Field {name} in message {message.Descriptor.Name} has no accessor");
                }
                switch (accessor.GetValue(message))
                {
                    case string text:
                        return text;
                    case IMessage nestedMessage:
                        message = nestedMessage;
                        break;
                    case null:
                        // Probably a nested message, but with no value. Just don't use an affinity key.
                        return null;
                    default:
                        throw new InvalidOperationException($"Field {name} in message {message.Descriptor.Name} is neither a string nor another message");
                }
            }
            throw new InvalidOperationException($"Affinity key {affinityKey} in message {message.Descriptor.Name} does not represent a path to a string value");
        }

        private void Bind(ChannelRef channelRef, string affinityKey)
        {
            if (!string.IsNullOrEmpty(affinityKey))
            {
                lock (thisLock)
                {
                    // TODO: What should we do if the dictionary already contains this key, but for a different channel ref?
                    if (!channelRefByAffinityKey.Keys.Contains(affinityKey))
                    {
                        channelRefByAffinityKey.Add(affinityKey, channelRef);
                    }
                    channelRefByAffinityKey[affinityKey].AffinityCountIncr();
                }
            }
        }

        private void Unbind(string affinityKey)
        {
            if (!string.IsNullOrEmpty(affinityKey))
            {
                lock (thisLock)
                {
                    if (channelRefByAffinityKey.TryGetValue(affinityKey, out ChannelRef channelRef))
                    {
                        int newCount = channelRef.AffinityCountDecr();

                        // We would expect it to be exactly 0, but it doesn't hurt to be cautious.
                        if (newCount <= 0)
                        {
                            channelRefByAffinityKey.Remove(affinityKey);
                        }
                    }
                }
            }
        }

        private Tuple<ChannelRef, string> PreProcess<TRequest>(AffinityConfig affinityConfig, TRequest request)
        {
            // Gets the affinity bound key if required in the request method.
            string boundKey = null;
            if (affinityConfig != null)
            {
                if (affinityConfig.Command == AffinityConfig.Types.Command.Bound
                    || affinityConfig.Command == AffinityConfig.Types.Command.Unbind)
                {
                    boundKey = GetAffinityKeyFromProto(affinityConfig.AffinityKey, (IMessage)request);
                }
            }
            ChannelRef channelRef = GetChannelRef(boundKey);
            channelRef.ActiveStreamCountIncr();
            return new Tuple<ChannelRef, string>(channelRef, boundKey);
        }

        // Note: response may be default(TResponse) in the case of a failure. We only expect to be called from
        // protobuf-based calls anyway, so it will always be a class type, and will never be null for success cases.
        // We can therefore check for nullity rather than having a separate "success" parameter.
        private void PostProcess<TResponse>(AffinityConfig affinityConfig, ChannelRef channelRef, string boundKey, TResponse response)
        {
            channelRef.ActiveStreamCountDecr();
            // Process BIND or UNBIND if the method has affinity feature enabled, but only for successful calls.
            if (affinityConfig != null && response != null)
            {
                if (affinityConfig.Command == AffinityConfig.Types.Command.Bind)
                {
                    Bind(channelRef, GetAffinityKeyFromProto(affinityConfig.AffinityKey, (IMessage)response));
                }
                else if (affinityConfig.Command == AffinityConfig.Types.Command.Unbind)
                {
                    Unbind(boundKey);
                }
            }            
            
        }

        /// <summary>
        /// Invokes a client streaming call asynchronously.
        /// In client streaming scenario, client sends a stream of requests and server responds with a single response.
        /// </summary>
        public override AsyncClientStreamingCall<TRequest, TResponse>
            AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
        {
            // No channel affinity feature for client streaming call.
            ChannelRef channelRef = GetChannelRef();
            var callDetails = new CallInvocationDetails<TRequest, TResponse>(channelRef.Channel, method, host, options);
            var originalCall = Calls.AsyncClientStreamingCall(callDetails);

            // Decrease the active streams count once async response finishes.
            var gcpResponseAsync = DecrementCountAndPropagateResult(originalCall.ResponseAsync);

            // Create a wrapper of the original AsyncClientStreamingCall.
            return new AsyncClientStreamingCall<TRequest, TResponse>(
                originalCall.RequestStream,
                gcpResponseAsync,
                originalCall.ResponseHeadersAsync,
                () => originalCall.GetStatus(),
                () => originalCall.GetTrailers(),
                () => originalCall.Dispose());

            async Task<TResponse> DecrementCountAndPropagateResult(Task<TResponse> task)
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                finally
                {
                    channelRef.ActiveStreamCountDecr();
                }
            }
        }

        /// <summary>
        /// Invokes a duplex streaming call asynchronously.
        /// In duplex streaming scenario, client sends a stream of requests and server responds with a stream of responses.
        /// The response stream is completely independent and both side can be sending messages at the same time.
        /// </summary>
        public override AsyncDuplexStreamingCall<TRequest, TResponse>
            AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
        {
            // No channel affinity feature for duplex streaming call.
            ChannelRef channelRef = GetChannelRef();
            var callDetails = new CallInvocationDetails<TRequest, TResponse>(channelRef.Channel, method, host, options);
            var originalCall = Calls.AsyncDuplexStreamingCall(callDetails);

            // Decrease the active streams count once the streaming response finishes its final batch.
            var gcpResponseStream = new GcpClientResponseStream<TRequest, TResponse>(
                originalCall.ResponseStream,
                (resp) => channelRef.ActiveStreamCountDecr());

            // Create a wrapper of the original AsyncDuplexStreamingCall.
            return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                originalCall.RequestStream,
                gcpResponseStream,
                originalCall.ResponseHeadersAsync,
                () => originalCall.GetStatus(),
                () => originalCall.GetTrailers(),
                () => originalCall.Dispose());
        }

        /// <summary>
        /// Invokes a server streaming call asynchronously.
        /// In server streaming scenario, client sends on request and server responds with a stream of responses.
        /// </summary>
        public override AsyncServerStreamingCall<TResponse>
            AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            affinityByMethod.TryGetValue(method.FullName, out AffinityConfig affinityConfig);

            Tuple<ChannelRef, string> tupleResult = PreProcess(affinityConfig, request);
            ChannelRef channelRef = tupleResult.Item1;
            string boundKey = tupleResult.Item2;

            var callDetails = new CallInvocationDetails<TRequest, TResponse>(channelRef.Channel, method, host, options);
            var originalCall = Calls.AsyncServerStreamingCall(callDetails, request);

            // Executes affinity postprocess once the streaming response finishes its final batch.
            var gcpResponseStream = new GcpClientResponseStream<TRequest, TResponse>(
                originalCall.ResponseStream,
                (resp) => PostProcess(affinityConfig, channelRef, boundKey, resp));

            // Create a wrapper of the original AsyncServerStreamingCall.
            return new AsyncServerStreamingCall<TResponse>(
                gcpResponseStream,
                originalCall.ResponseHeadersAsync,
                () => originalCall.GetStatus(),
                () => originalCall.GetTrailers(),
                () => originalCall.Dispose());

        }

        /// <summary>
        /// Invokes a simple remote call asynchronously.
        /// </summary>
        public override AsyncUnaryCall<TResponse>
            AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            affinityByMethod.TryGetValue(method.FullName, out AffinityConfig affinityConfig);

            Tuple<ChannelRef, string> tupleResult = PreProcess(affinityConfig, request);
            ChannelRef channelRef = tupleResult.Item1;
            string boundKey = tupleResult.Item2;

            var callDetails = new CallInvocationDetails<TRequest, TResponse>(channelRef.Channel, method, host, options);
            var originalCall = Calls.AsyncUnaryCall(callDetails, request);

            // Executes affinity postprocess once the async response finishes.
            var gcpResponseAsync = PostProcessPropagateResult(originalCall.ResponseAsync);

            // Create a wrapper of the original AsyncUnaryCall.
            return new AsyncUnaryCall<TResponse>(
                gcpResponseAsync,
                originalCall.ResponseHeadersAsync,
                () => originalCall.GetStatus(),
                () => originalCall.GetTrailers(),
                () => originalCall.Dispose());

            async Task<TResponse> PostProcessPropagateResult(Task<TResponse> task)
            {
                TResponse response = default(TResponse);
                try
                {
                    response = await task.ConfigureAwait(false);
                    return response;
                }
                finally
                {
                    PostProcess(affinityConfig, channelRef, boundKey, response);
                }
            }
        }

        /// <summary>
        /// Invokes a simple remote call in a blocking fashion.
        /// </summary>
        public override TResponse
            BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            affinityByMethod.TryGetValue(method.FullName, out AffinityConfig affinityConfig);

            Tuple<ChannelRef, string> tupleResult = PreProcess(affinityConfig, request);
            ChannelRef channelRef = tupleResult.Item1;
            string boundKey = tupleResult.Item2;

            var callDetails = new CallInvocationDetails<TRequest, TResponse>(channelRef.Channel, method, host, options);
            TResponse response = default(TResponse);
            try
            {
                response = Calls.BlockingUnaryCall<TRequest, TResponse>(callDetails, request);
                return response;
            }
            finally
            {
                PostProcess(affinityConfig, channelRef, boundKey, response);
            }
        }

        /// <summary>
        /// Shuts down the all channels in the underlying channel pool cleanly. It is strongly
        /// recommended to shutdown all previously created channels before exiting from the process.
        /// </summary>
        public async Task ShutdownAsync()
        {
            for (int i = 0; i < channelRefs.Count; i++)
            {
                await channelRefs[i].Channel.ShutdownAsync();
            }
        }

        // Test helper methods

        /// <summary>
        /// Returns a deep clone of the internal list of channel references.
        /// This method should only be used in tests.
        /// </summary>
        internal IList<ChannelRef> GetChannelRefsForTest()
        {
            lock (thisLock)
            {
                // Create an independent copy
                return channelRefs.Select(cr => cr.Clone()).ToList();
            }
        }

        /// <summary>
        /// Returns a deep clone of the internal dictionary of channel references by affinity key.
        /// This method should only be used in tests.
        /// </summary>
        internal IDictionary<string, ChannelRef> GetChannelRefsByAffinityKeyForTest()
        {
            lock (thisLock)
            {
                // Create an independent copy
                return channelRefByAffinityKey.ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
            }
        }
    }
}
