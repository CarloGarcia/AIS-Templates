Feature: Customers
	In order to avoid silly mistakes
	As a math idiot
	I want to be told the sum of two numbers

@mytag
Scenario: Test Mock HTTP Server
	Given a local service is exposed on subdomain aistemplates and port 8088
	And an external Rest Endpoint is setup on Url http://aistemplates.serveo.net/SendData/ for host aistemplates that responds with HTTP status code 200 with the following response body ..//..//..//TestInput//customer_response.json
	When the following request "..//..//..//TestInput//customer_request.json" is sent to Url "http://aistemplates.serveo.net/SendData/"
	Then the Rest EndPoint received a message with the content as in file "..//..//..//TestInput//customer_response.json" within 10 seconds
