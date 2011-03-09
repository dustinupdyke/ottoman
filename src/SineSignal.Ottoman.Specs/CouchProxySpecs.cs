using System;
using System.Net;

using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using SineSignal.Ottoman.Specs.Framework;

using SineSignal.Ottoman.Commands;
using SineSignal.Ottoman.Exceptions;
using SineSignal.Ottoman.Http;

namespace SineSignal.Ottoman.Specs
{
	public class CouchProxySpecs
	{
		public class When_executing_a_couch_command_with_a_message : ConcernFor<CouchProxy>
		{
			private Employee entity1;
			private IRestClient restClient;
			private ICouchCommand couchCommand;
			private ResultStub resultStub;
			private RestResponse<ResultStub> restResponse;
			
			protected override void Given()
			{
				entity1 = new Employee { Name = "Bob", Login = "boblogin" };
				restClient = Fake<IRestClient>();
				couchCommand = Fake<ICouchCommand>();
				couchCommand.Route.Returns("some/path");
				couchCommand.Operation.Returns(HttpMethod.Post);
				couchCommand.Message.Returns(entity1);
				couchCommand.SuccessStatusCode.Returns(HttpStatusCode.Created);
				
				restResponse = new RestResponse<ResultStub>
				{
					ContentType = "application/json",
					ContentLength = 5,
					Content = "{\"status\":\"completed\"}",
					StatusCode = HttpStatusCode.Created,
					StatusDescription = HttpStatusCode.Created.ToString(),
					ContentDeserialized = new ResultStub()
				};
				
				restClient.Process<ResultStub>(Arg.Any<RestRequest>(), Arg.Any<HttpStatusCode>()).Returns(restResponse);
			}
			
			public override CouchProxy CreateSystemUnderTest()
			{
				return new CouchProxy(restClient);
			}

			protected override void When()
			{
				resultStub = Sut.Execute<ResultStub>(couchCommand);
			}
			
			[Test]
			public void Should_call_process_on_rest_client_with_generated_rest_request()
			{
				restClient.Received().Process<ResultStub>(
					Arg.Is<RestRequest>(r => r.Path == "some/path" && 
						r.Method == HttpMethod.Post && 
						(r.Payload is Employee && r.Payload == entity1)
					), 
					HttpStatusCode.Created
				);
			}
			
			[Test]
			public void Should_return_deserialized_object()
			{
				Assert.That(resultStub.Status, Is.EqualTo("completed"));
			}
		}
		
		public class When_executing_a_couch_command_without_a_message : ConcernFor<CouchProxy>
		{
			private IRestClient restClient;
			private ICouchCommand couchCommand;
			private ResultStub resultStub;
			private RestResponse<ResultStub> restResponse;
			
			protected override void Given()
			{
				restClient = Fake<IRestClient>();
				couchCommand = Fake<ICouchCommand>();
				couchCommand.Route.Returns("some/path");
				couchCommand.Operation.Returns(HttpMethod.Get);
				couchCommand.SuccessStatusCode.Returns(HttpStatusCode.OK);
				
				restResponse = new RestResponse<ResultStub>
				{
					ContentType = "application/json",
					ContentLength = 5,
					Content = "{\"status\":\"completed\"}",
					StatusCode = HttpStatusCode.OK,
					StatusDescription = HttpStatusCode.OK.ToString(),
					ContentDeserialized = new ResultStub()
				};
				
				restClient.Process<ResultStub>(Arg.Any<RestRequest>(), Arg.Any<HttpStatusCode>()).Returns(restResponse);
			}
			
			public override CouchProxy CreateSystemUnderTest()
			{
				return new CouchProxy(restClient);
			}

			protected override void When()
			{
				resultStub = Sut.Execute<ResultStub>(couchCommand);
			}
			
			[Test]
			public void Should_call_process_on_rest_client_with_generated_rest_request()
			{
				restClient.Received().Process<ResultStub>(
					Arg.Is<RestRequest>(r => r.Path == "some/path" && 
						r.Method == HttpMethod.Get && 
						r.Payload == null
					), 
					HttpStatusCode.OK
				);
			}
			
			[Test]
			public void Should_return_deserialized_object()
			{
				Assert.That(resultStub.Status, Is.EqualTo("completed"));
			}
		}
		
		public class When_executing_a_command_that_causes_an_unexpected_response_by_rest_client : ConcernFor<CouchProxy>
		{
			private IRestClient restClient;
			private ICouchCommand couchCommand;
			private Exception expectedException;
			
			protected override void Given()
			{
				restClient = Fake<IRestClient>();
				
				couchCommand = Fake<ICouchCommand>();
				couchCommand.Route.Returns("some/path");
				couchCommand.Operation.Returns(HttpMethod.Get);
				couchCommand.SuccessStatusCode.Returns(HttpStatusCode.OK);
				couchCommand.When(c => c.HandleError(Arg.Any<string>(), Arg.Any<CommandErrorResult>(), Arg.Any<UnexpectedHttpResponseException>())).Do(e => {
					throw new Exception("Command Exception");
				});
				
				restClient.BaseUri.Returns(new Uri("http://127.0.0.1:5984"));
				restClient.Process<ResultStub>(Arg.Any<RestRequest>(), Arg.Any<HttpStatusCode>()).Returns(e => { 
					throw new UnexpectedHttpResponseException(HttpStatusCode.OK, new HttpResponse { Error = new Exception("Some exception message") });
				});
			}
			
			public override CouchProxy CreateSystemUnderTest()
			{
				return new CouchProxy(restClient);
			}
			
			protected override void When()
			{
				try
				{
					Sut.Execute<ResultStub>(couchCommand);
				}
				catch (Exception e)
				{
					expectedException = e;
				}
			}
			
			[Test]
			public void Should_call_process_on_rest_client_with_generated_rest_request()
			{
				restClient.Received().Process<ResultStub>(
					Arg.Is<RestRequest>(r => r.Path == "some/path" && 
						r.Method == HttpMethod.Get && 
						r.Payload == null
					), 
					HttpStatusCode.OK
				);
			}
			
			[Test]
			public void Should_call_error_handler_on_couch_command()
			{
				couchCommand.Received().HandleError(Arg.Any<string>(), Arg.Any<CommandErrorResult>(), Arg.Any<UnexpectedHttpResponseException>());
			}
			
			[Test]
			public void Should_receive_exception_from_handle_error()
			{
				Assert.That(expectedException.Message, Is.EqualTo("Command Exception"));
			}
		}
	}
	
	public class ResultStub
	{
		public string Status { get; private set; }
			
		public ResultStub()
		{
			Status = "completed";
		}
	}
}
