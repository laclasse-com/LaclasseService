FROM mono:latest
COPY LaclasseService/bin/Debug /app
VOLUME /var/lib/laclasse
CMD [ "mono", "./app/LaclasseService.exe", "-c", "/var/lib/laclasse/etc/laclasse.conf" ]
