import json
import re
import argparse
import os

def load_parameters(parameters_file):
    # Load parameters from JSON file
    with open(parameters_file, 'r') as file:
        parameters = json.load(file)

    # Check if all parameters have a value
    for param, value in parameters.items():
        if value == "":
            raise ValueError(f"Value for parameter '{param}' is missing")
    
    return parameters

def replace_parameters_in_string(s, parameters):
    # Find all parameters in the string
    string_params = re.findall(r'<(.*?)>', s)
    print(s)

    # Check if all parameters in the string have a corresponding value
    for param in string_params:
        if param not in parameters:
            raise ValueError(f"Value for parameter '{param}' in the template is missing")

    # Replace parameters in the string
    for param in string_params:
        print(param)
        value = parameters[param]
        print(f"Replacing parameter '{param} with value '{value}'")
        s = re.sub(f'<{param}>', value, s)

    return s

def process_aci_yaml(template_file, parameters_file, output_file):
    parameters = load_parameters(parameters_file)

    # Load YAML template
    with open(template_file, 'r') as file:
        yaml_template = file.read()

    # Replace parameters in YAML template
    yaml_template = replace_parameters_in_string(yaml_template, parameters)

    # Check if output file already exists
    if os.path.exists(output_file):
        overwrite = input(f"{output_file} already exists. Do you want to overwrite it? (y/n): ")
        if overwrite.lower() != 'y':
            print("Not overwriting existing file. Exiting...")
            return

    # Write the resulting YAML to a new file
    with open(output_file, 'w') as file:
        file.write(yaml_template)

def process_launchsettings_json(template_file, parameters_file, profile):
    parameters = load_parameters(parameters_file)

    # Load JSON template
    with open(template_file, 'r') as file:
        json_template = json.load(file)

    output_file = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Properties", "launchSettings.json")


    # Check if output file already exists, if not create an empty one
    if not os.path.exists(output_file):
        with open(output_file, 'w') as file:
            json.dump({"profiles": {}}, file)

    # Load existing launchsettings
    with open(output_file, 'r') as file:
        json_existing = json.load(file)

    # Iterate over each profile in the template
    for profile_name, settings in json_template['profiles'].items():
        if profile and profile != profile_name:
            continue
        # Iterate over each setting in the profile
        for setting, value in settings["environmentVariables"].items():
            if isinstance(value, str):
                # If the value is a string and contains a parameter, replace it
                print(f"Replacing parameter in {profile_name} - {setting}")
                value = replace_parameters_in_string(value, parameters)
                settings["environmentVariables"][setting] = value

        # Update or add the profile in the existing launchsettings
        json_existing['profiles'][profile_name] = settings

    # Write the resulting JSON to the output file
    with open(output_file, 'w') as file:
        json.dump(json_existing, file, indent=2)

if __name__ == "__main__":
    # Define command-line arguments
    parser = argparse.ArgumentParser()
    parser.add_argument("--template-file", required=True)
    parser.add_argument("--parameters-file", default="deployment-params.json")
    parser.add_argument("--output-file")
    parser.add_argument("--profile")
    args = parser.parse_args()

    # Check the file extension of the template file to determine its type
    if args.template_file.endswith('.yaml'):
        # Run the function for YAML files
        output_file_name = args.output_file or "aci_feployment.yaml"
        process_aci_yaml(args.template_file, args.parameters_file, output_file_name)
    elif args.template_file.endswith('.json'):
        # Run the function for JSON files
        process_launchsettings_json(args.template_file, args.parameters_file, args.profile)
