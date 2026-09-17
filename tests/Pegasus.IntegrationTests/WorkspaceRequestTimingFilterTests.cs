using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Pegasus.Core.Documents;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

public sealed class WorkspaceRequestTimingFilterTests
{
    [Theory]
    [InlineData("/Cases/Details", null, null, "web.case.resource", "web.case.result")]
    [InlineData("/Cases/Details", "Section", null, "web.case.section.resource", "web.case.section.result")]
    [InlineData("/Cases/Details", null, "section", "web.case.section.resource", "web.case.section.result")]
    [InlineData("/Index", null, null, "web.workcentre.resource", "web.workcentre.result")]
    [InlineData("/Index", null, "Refresh", "web.workcentre.refresh.resource", "web.workcentre.refresh.result")]
    [InlineData("/Search", null, null, null, null)]
    public async Task TimingsEncloseActivationAndResultWithoutEmittingRouteValues(
        string page, string? routeHandler, string? queryHandler, string? resourcePhase, string? resultPhase)
    {
        using var listener = Listen();
        using var request = new Activity("request").Start();
        var context = Context(page, routeHandler, queryHandler);
        var filter = new WorkspaceRequestTimingFilter();
        Activity? resource = null;
        Activity? result = null;

        await filter.OnResourceExecutionAsync(new ResourceExecutingContext(context, [], []), async () =>
        {
            resource = Activity.Current;
            var pageResult = new PageResult();
            await filter.OnResultExecutionAsync(new ResultExecutingContext(context, [], pageResult, new object()), () =>
            {
                result = Activity.Current;
                return Task.FromResult(new ResultExecutedContext(context, [], pageResult, new object()));
            });
            Assert.Same(resource, Activity.Current);
            return new ResourceExecutedContext(context, []);
        });

        Assert.Same(request, Activity.Current);
        if (resourcePhase is null)
        {
            Assert.Same(request, resource);
            Assert.Same(request, result);
        }
        else
        {
            Assert.NotNull(resource);
            Assert.NotNull(result);
            Assert.Equal(resourcePhase, resource.OperationName);
            Assert.Equal(resultPhase, result.OperationName);
            Assert.Equal(request.Id, resource.ParentId);
            Assert.Equal(resource.Id, result.ParentId);
            Assert.Empty(resource.TagObjects);
            Assert.Empty(result.TagObjects);
        }
    }

    [Fact]
    public async Task FailedActivationStopsTheResourceSpanAndPreservesTheException()
    {
        using var listener = Listen();
        using var request = new Activity("request").Start();
        var context = Context("/Cases/Details", null, null);
        var failure = new InvalidOperationException("activation failed");
        Activity? resource = null;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkspaceRequestTimingFilter().OnResourceExecutionAsync(
                new ResourceExecutingContext(context, [], []), () =>
                {
                    resource = Activity.Current;
                    throw failure;
                }));

        Assert.Same(failure, thrown);
        Assert.Same(request, Activity.Current);
        Assert.NotNull(resource);
        Assert.True(resource.IsStopped);
    }

    private static ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DocumentReadTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ActionContext Context(string page, string? routeHandler, string? queryHandler)
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = queryHandler is null ? QueryString.Empty : new QueryString("?handler=" + queryHandler);
        var route = new RouteData();
        route.Values["handler"] = routeHandler;
        route.Values["id"] = Guid.NewGuid();
        return new ActionContext(http, route, new PageActionDescriptor { ViewEnginePath = page });
    }
}
