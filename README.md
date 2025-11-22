# DbMetaTool

DbMetaTool to aplikacja konsolowa w C# (.NET 8.0) służąca do pracy ze strukturą baz danych Firebird 5.0. Umożliwia tworzenie nowej bazy z plików SQL, eksport metadanych z istniejącej bazy oraz aktualizowanie jej na podstawie dostarczonych skryptów. Aplikacja obsługuje domeny, tabele oraz procedury.


## Działanie aplikacji

### 1. Tworzenie nowej bazy – `build-db`

**Opis:**  
Tworzy nową bazę danych Firebird 5.0 w określonym katalogu i wykonuje skrypty SQL z podanego katalogu.

**Parametry:**

- `databaseDirectory` – katalog, w którym ma zostać utworzona baza danych.
- `scriptsDirectory` – katalog zawierający skrypty SQL (tylko domeny, tabele i procedury).

**Działanie:**

1. Tworzy katalog bazy danych, jeśli nie istnieje.
2. Generuje pustą bazę danych Firebird (`.fdb`) w podanym katalogu.
3. Wczytuje i wykonuje wszystkie pliki `.sql` z katalogu skryptów.
4. Raportuje poprawnie wykonane skrypty oraz ewentualne błędy.

---

### 2. Eksport metadanych – `export-scripts`

**Opis:**  
Eksportuje metadane domen, tabel i procedur z istniejącej bazy danych do plików SQL w określonym katalogu.

**Parametry:**

- `connectionString` – połączenie do istniejącej bazy danych.
- `outputDirectory` – katalog, w którym zostaną zapisane wygenerowane pliki.

**Działanie:**

1. Łączy się z bazą danych przy użyciu connection string.
2. Pobiera informacje o domenach, tabelach i procedurach.
3. Generuje pliki `.sql` dla każdej domeny, tabeli i procedury, zachowując strukturę SQL.
4. Zapisuje pliki w katalogu wyjściowym.

---

### 3. Aktualizacja bazy – `update-db`

**Opis:**  
Aktualizuje istniejącą bazę danych na podstawie zestawu skryptów SQL.

**Parametry:**

- `connectionString` – połączenie do istniejącej bazy danych.
- `scriptsDirectory` – katalog zawierający skrypty SQL (domeny, tabele, procedury).

**Działanie:**

1. Grupuje skrypty według typu obiektu: domeny → tabele → procedury.
2. Wykonuje skrypty w odpowiedniej kolejności w ramach jednej transakcji.
3. Jeśli którykolwiek skrypt zakończy się błędem, wszystkie zmiany są wycofywane (`ROLLBACK`), zapewniając bezpieczeństwo bazy.
4. Wyświetla raport wykonania skryptów.

---

## Sposób użycia

1. Budowa nowej bazy danych
Tworzy pustą bazę Firebird i wykonuje wszystkie skrypty SQL z podanego katalogu.

```bash
dotnet run --project <ścieżka_do_projektu> -- build-db --db-dir "<katalog_bazy>" --scripts-dir "<katalog_skryptów>"
```
2. Eksport metadanych z istniejącej bazy
```bash
dotnet run --project <ścieżka_do_projektu> -- export-scripts --connection-string "<connection_string>" --output-dir "<katalog_wyjściowy>"
```
3. Aktualizacja istniejącej bazy na podstawie skryptów
```bash
dotnet run --project <ścieżka_do_projektu> -- update-db `--connection-string "<connection_string>"` --scripts-dir "<katalog_skryptów>"
```
