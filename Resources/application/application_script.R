# setwd("C:/user/tasks/shared")

# Load package "matlab"
if (!is.element("matlab", installed.packages())) {
  install.packages("matlab",repos="http://cran.rstudio.com/")
}
library("matlab")

# Load the input file into a data frame
inputfile = dir(pattern = "*.csv")
input <- read.csv(inputfile)


# Apply the isprime function to each row of the input data frame and export
output <- data.frame(input,apply(input,1,isprime))
colnames(output) <- c("number","isprime")
output<-output[output$isprime==1,]

write.csv(output, file="output.csv",row.names = F)




