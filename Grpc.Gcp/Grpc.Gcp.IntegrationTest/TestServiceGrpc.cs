// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: test_service.proto
// </auto-generated>
// Original file comments:
//
// Copyright 2018 Google Inc. All Rights Reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file or at
// https://developers.google.com/open-source/licenses/bsd
//
#pragma warning disable 1591
#region Designer generated code

using grpc = global::Grpc.Core;

namespace Grpc.Gcp.IntegrationTest {
  public static partial class TestService
  {
    static readonly string __ServiceName = "grpc.gcp.integration_test.TestService";

    static readonly grpc::Marshaller<global::Grpc.Gcp.IntegrationTest.SimpleRequest> __Marshaller_SimpleRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Grpc.Gcp.IntegrationTest.SimpleRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::Grpc.Gcp.IntegrationTest.SimpleResponse> __Marshaller_SimpleResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Grpc.Gcp.IntegrationTest.SimpleResponse.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::Grpc.Gcp.IntegrationTest.ComplexRequest> __Marshaller_ComplexRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Grpc.Gcp.IntegrationTest.ComplexRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::Grpc.Gcp.IntegrationTest.ComplexResponse> __Marshaller_ComplexResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Grpc.Gcp.IntegrationTest.ComplexResponse.Parser.ParseFrom);

    static readonly grpc::Method<global::Grpc.Gcp.IntegrationTest.SimpleRequest, global::Grpc.Gcp.IntegrationTest.SimpleResponse> __Method_DoSimple = new grpc::Method<global::Grpc.Gcp.IntegrationTest.SimpleRequest, global::Grpc.Gcp.IntegrationTest.SimpleResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "DoSimple",
        __Marshaller_SimpleRequest,
        __Marshaller_SimpleResponse);

    static readonly grpc::Method<global::Grpc.Gcp.IntegrationTest.ComplexRequest, global::Grpc.Gcp.IntegrationTest.ComplexResponse> __Method_DoComplex = new grpc::Method<global::Grpc.Gcp.IntegrationTest.ComplexRequest, global::Grpc.Gcp.IntegrationTest.ComplexResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "DoComplex",
        __Marshaller_ComplexRequest,
        __Marshaller_ComplexResponse);

    /// <summary>Service descriptor</summary>
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::Grpc.Gcp.IntegrationTest.TestServiceReflection.Descriptor.Services[0]; }
    }

    /// <summary>Base class for server-side implementations of TestService</summary>
    public abstract partial class TestServiceBase
    {
      public virtual global::System.Threading.Tasks.Task<global::Grpc.Gcp.IntegrationTest.SimpleResponse> DoSimple(global::Grpc.Gcp.IntegrationTest.SimpleRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::Grpc.Gcp.IntegrationTest.ComplexResponse> DoComplex(global::Grpc.Gcp.IntegrationTest.ComplexRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

    }

    /// <summary>Client for TestService</summary>
    public partial class TestServiceClient : grpc::ClientBase<TestServiceClient>
    {
      /// <summary>Creates a new client for TestService</summary>
      /// <param name="channel">The channel to use to make remote calls.</param>
      public TestServiceClient(grpc::Channel channel) : base(channel)
      {
      }
      /// <summary>Creates a new client for TestService that uses a custom <c>CallInvoker</c>.</summary>
      /// <param name="callInvoker">The callInvoker to use to make remote calls.</param>
      public TestServiceClient(grpc::CallInvoker callInvoker) : base(callInvoker)
      {
      }
      /// <summary>Protected parameterless constructor to allow creation of test doubles.</summary>
      protected TestServiceClient() : base()
      {
      }
      /// <summary>Protected constructor to allow creation of configured clients.</summary>
      /// <param name="configuration">The client configuration.</param>
      protected TestServiceClient(ClientBaseConfiguration configuration) : base(configuration)
      {
      }

      public virtual global::Grpc.Gcp.IntegrationTest.SimpleResponse DoSimple(global::Grpc.Gcp.IntegrationTest.SimpleRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DoSimple(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::Grpc.Gcp.IntegrationTest.SimpleResponse DoSimple(global::Grpc.Gcp.IntegrationTest.SimpleRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_DoSimple, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::Grpc.Gcp.IntegrationTest.SimpleResponse> DoSimpleAsync(global::Grpc.Gcp.IntegrationTest.SimpleRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DoSimpleAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::Grpc.Gcp.IntegrationTest.SimpleResponse> DoSimpleAsync(global::Grpc.Gcp.IntegrationTest.SimpleRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_DoSimple, null, options, request);
      }
      public virtual global::Grpc.Gcp.IntegrationTest.ComplexResponse DoComplex(global::Grpc.Gcp.IntegrationTest.ComplexRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DoComplex(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::Grpc.Gcp.IntegrationTest.ComplexResponse DoComplex(global::Grpc.Gcp.IntegrationTest.ComplexRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_DoComplex, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::Grpc.Gcp.IntegrationTest.ComplexResponse> DoComplexAsync(global::Grpc.Gcp.IntegrationTest.ComplexRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DoComplexAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::Grpc.Gcp.IntegrationTest.ComplexResponse> DoComplexAsync(global::Grpc.Gcp.IntegrationTest.ComplexRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_DoComplex, null, options, request);
      }
      /// <summary>Creates a new instance of client from given <c>ClientBaseConfiguration</c>.</summary>
      protected override TestServiceClient NewInstance(ClientBaseConfiguration configuration)
      {
        return new TestServiceClient(configuration);
      }
    }

    /// <summary>Creates service definition that can be registered with a server</summary>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    public static grpc::ServerServiceDefinition BindService(TestServiceBase serviceImpl)
    {
      return grpc::ServerServiceDefinition.CreateBuilder()
          .AddMethod(__Method_DoSimple, serviceImpl.DoSimple)
          .AddMethod(__Method_DoComplex, serviceImpl.DoComplex).Build();
    }

  }
}
#endregion
