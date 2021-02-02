const getApiKey = () => window.config.apiKey;
const getHost = () => window.config.host;
const getCurrentVariablesUrl = () => `${getHost()}/api/Spaces-1/variables/${vmBindings.project().VariableSetId}?apikey=${getApiKey()}`;
const getAllProjects = () => `${getHost()}/api/projects/all?apikey=${getApiKey()}`;

const requestParams = {
  method: "GET",
  headers: {
    accept: "application/json"    
  }
};

const loadProjects = async () => {
  let response = await fetch(getAllProjects(), requestParams);

  let projectNames = JSON.parse(await response.text()).map(x => ({
    Name: x.Name,
    VariableSetId: x.VariableSetId
  }));

  vmBindings.availableProjects(projectNames);
}

loadProjects();

const viewVariables = async () => {
  const currentVariablesResponse = await getCurrentVariablesForProject(); 

  const currentVariables = currentVariablesResponse.Variables.map(x => ({
    key: ko.observable(x.Name),
    value: ko.observable(`${x.Value}`)
  }));

  vmBindings.variables(currentVariables);
}

const getCurrentVariablesForProject = async () => {
  const currentConfig = await fetch(getCurrentVariablesUrl(), requestParams);

  return JSON.parse(await currentConfig.text());
}

function VmProperties() {  
  this.availableProjects = ko.observableArray([]);
  this.variables = ko.observableArray([]);
  this.project = ko.observable("");
  this.viewVariables = viewVariables;  
}

let vmBindings = new VmProperties();
ko.applyBindings(vmBindings);
