"D:\Program Files\R\R-3.4.0\bin\Rscript.exe" --vanilla %AZ_BATCH_NODE_SHARED_DIR%\application_script.R
cd %AZ_BATCH_TASK_WORKING_DIR%
%AZ_BATCH_NODE_SHARED_DIR%\7za.exe a %AZ_BATCH_TASK_ID%.zip output.csv
%AZ_BATCH_NODE_SHARED_DIR%\UploadToBlob.exe %AZ_BATCH_TASK_ID%.zip %1 %2
