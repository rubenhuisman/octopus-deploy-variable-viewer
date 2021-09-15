const getUrlsParams = () => {
    var paramsString = window.location.search.split("?")[1];
    var paramValues = paramsString.split("&");

    var params = new Array();

    paramValues.forEach(param => {
        var paramValue = param.split("=");
        params[paramValue[0]] = paramValue[1];
    });

    return params;
}


const getApiKey = () => getUrlsParams().apiKey;
const getHost = () => getUrlsParams().host;

const getCurrentVariablesUrl = () => `${getHost()}/api/Spaces-1/variables/${vmBindings.project().VariableSetId}?apikey=${getApiKey()}`;
const getAllProjects = () => `${getHost()}/api/projects/all?apikey=${getApiKey()}`;

const requestParams = {
    method: "GET",
    headers: {
        accept: "application/json",
        Origin: "x-requested-with"
    }
};

const loadProjects = async() => {
    let response = await fetch(getAllProjects(), requestParams);

    let projectNames = JSON.parse(await response.text()).map(x => ({
        Name: x.Name,
        VariableSetId: x.VariableSetId
    }));

    vmBindings.availableProjects(projectNames.sort((a, b) => a.Name.localeCompare(b.Name)));
}

loadProjects();

const viewVariables = async() => {
    const currentVariablesResponse = await getCurrentVariablesForProject();
    const environments = {};
    currentVariablesResponse.ScopeValues.Environments.forEach(x => {
        environments[x.Id] = x.Name;
    });

    const currentVariables = currentVariablesResponse.Variables
        .map(x => ({
            key: ko.observable(x.Name),
            value: ko.observable(`${x.Value}`),
            enviromentName: `(${x.Scope.Environment.map(x => environments[x]).join(", ")})`
        }))
        .filter(x => x.enviromentName.includes(vmBindings.environment()));


    vmBindings.variables(currentVariables);
    vmBindings.environments(Object.values(environments))
}

const getCurrentVariablesForProject = async() => {
    const currentConfig = await fetch(getCurrentVariablesUrl(), requestParams);

    return JSON.parse(await currentConfig.text());
}

function VmProperties() {
    this.availableProjects = ko.observableArray([]);
    this.environments = ko.observableArray([]);
    this.variables = ko.observableArray([]);
    this.project = ko.observable("");
    this.environment = ko.observable("");

    this.viewVariables = viewVariables;
}

let vmBindings = new VmProperties();
ko.applyBindings(vmBindings);