# Use the .NET 6 SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the .csproj file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the entire project and build the application
COPY . ./
RUN dotnet publish -c Release -o out

# Use a lighter runtime image for the final image
FROM mcr.microsoft.com/dotnet/runtime:6.0

# Set the working directory inside the container
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/out .

# Set the entry point for the container
ENTRYPOINT ["dotnet", "TextAnalyticsHealthcareAdaptiveClient.dll"]