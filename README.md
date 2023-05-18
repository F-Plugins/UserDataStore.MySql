# UserDataStore.MySql

This plugin implements the openmod user data store system with MySql.

You might need this to sync roles and permissions in multiple servers.

The biggest problem this solves is the users data .yaml file getting too big and slowing down the performance of the server

If you need help join: discord.fplugins.com

## Install Command
```
openmod install Feli.UserDataStore.MySql
```

## Configuration
```yml
Database:
  ConnectionStrings:
    Default: "Server=127.0.0.1; Database=openmod; Port=3306; User=openmod; Password=password"

Cache:
  UseCache: true 
  RefreshInterval: 1200 # In Seconds
```
