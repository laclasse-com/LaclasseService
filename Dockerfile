FROM mono:latest AS build-env
WORKDIR /app

# copy everything and build
COPY . ./
RUN msbuild LaclasseService/LaclasseService.csproj

# build runtime image
FROM mono:latest
WORKDIR /app
COPY --from=build-env /app/LaclasseService/bin/Debug .
VOLUME /var/lib/laclasse
CMD [ "mono", "/app/LaclasseService.exe", "-c", "/var/lib/laclasse/etc/laclasse.conf" ]
