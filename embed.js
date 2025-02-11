function getEmbeddedReport() {
    // 1 - Get DOM object for div that is report container
    let reportContainer = document.getElementById("embed-container");

    // 2 - Get report embedding data from view model
    let reportId = window.viewModel.reportId;
    let embedUrl = window.viewModel.embedUrl;
    let token = window.viewModel.token;

    // 3 - Embed report using the Power BI JavaScript API.
    let models = window['powerbi-client'].models;
    let config = {
        type: 'report',
        id: reportId,
        embedUrl: embedUrl,
        accessToken: token,
        permissions: models.Permissions.All,
        tokenType: models.TokenType.Embed,
        viewMode: models.ViewMode.View,
        background: models.BackgroundType.Transparent,
        pageView: 'actualSize',
        slicers: window.viewModel.slicers,
        settings: {

            panes: {
                filters: { expanded: false, visible: false },
                pageNavigation: { visible: false }
            },
            hyperlinkClickBehavior: models.HyperlinkClickBehavior.RaiseEvent
        }
    };

    // Embed the report and display it within the div container.
    let report = powerbi.embed(reportContainer, config);

    // 4 - Add logic to resize embed container on window resize event
    let heightBuffer = 12;
    let newHeight = $(window).height() - ($("header").height() + heightBuffer);
   // $("#embed-container").height(newHeight);
    $(window).resize(function () {
        var newHeight = $(window).height() - ($("header").height() + heightBuffer);
    });

    return report;

}