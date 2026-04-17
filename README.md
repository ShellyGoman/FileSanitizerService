# File Sanitizer Service

A REST API microservice that accepts a file upload, detects its format, sanitizes any malicious content, and returns the clean file as a download.

Currently supports the **ABC format** - a format with a header, data blocks, and a footer. The architecture is built so adding new formats is straightforward.

---

## How it works

The service reads the file in a **single streaming pass** - it never loads the whole thing into memory. This means it handles very large files (500 MB by default) without breaking a sweat. Each data block is validated on the fly, malicious bytes get replaced, and the sanitized content is written out as it goes.

ABC files are made up of 3-byte blocks (`A<byte>C`). Any block where the middle byte isn't a digit (`1`–`9`) is considered malicious and gets replaced with `A255C`.

---

## Running the service

```bash
dotnet run --project FileSanitizerService.Api
```

The API will be available at `http://localhost:5000` (or `https://localhost:5001`).  
Swagger UI is available at `/swagger` when running in Development mode.

### Run with Docker Compose

```bash
docker compose -f docker-compose.yml up --build -d
```

The API will be available at `http://localhost:8080`.

To stop and remove containers/volumes:

```bash
docker compose -f docker-compose.yml down -v
```

---

## Sending a file

### Using Postman

1. Open Postman and create a new **POST** request to:
   ```
   http://localhost:5000/api/sanitize
   ```
2. Go to the **Body** tab → select **form-data**
3. Add a key named `file`, change its type to **File**, and attach your `.abc` file
4. Hit **Send** - the response will be the sanitized file as a download

---

### Using a Python script

```python
import requests

with open("examples/malicious_small.abc", "rb") as f:
    response = requests.post(
        "http://localhost:5000/api/sanitize",
        files={"file": ("malicious_small.abc", f, "application/octet-stream")}
    )

if response.status_code == 200:
    with open("sanitized_output.abc", "wb") as out:
        out.write(response.content)
    print("Done! Saved to sanitized_output.abc")
else:
    print(f"Error {response.status_code}: {response.text}")
```

---

## Example files

Check the `examples/` folder for ready-to-use test files:

| File | Description |
|------|------------|
| `benign_small.abc` | Small file with only valid blocks - comes back unchanged |
| `malicious_small.abc` | Small file with bad blocks - those get replaced |
| `big_mixed.abc` | Large file with a mix of good and bad blocks |
| `no_data.abc` | Valid structure but empty data section |
| `empty.abc` | Empty file |
| `bad_data_format.abc` | Structurally broken |

